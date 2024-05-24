﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet.Actions;
using Contracts;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Services.Contracts;
using SharedAPI.DataTransfer;
using SharedAPI.RequestFeatures;
using Folder = Entities.Models.Folder;

namespace Services
{
    public class FolderService : IFolderService
    {
        private readonly IRepositoryManager manager;
        private readonly UserManager<User> userManager;
        private readonly IMapper mapper;

        public FolderService(IRepositoryManager manager, UserManager<User> userManager, IMapper mapper)
        {
            this.manager = manager;
            this.userManager = userManager;
            this.mapper = mapper;
        }

        public async Task CreateFolderAsync(string username, CreateFolderDto folder)
        {
            var user = await userManager.FindByNameAsync(username);

            List<Folder> folders = null;
            if (folder.BaseFolderId.Equals(Guid.Empty))
            {
                folders = await manager.folder.GetFoldersByUser(user.Id, trackChanges: false);
            }
            else
            {
                folders = await manager.folder.GetChildFolders(folder.BaseFolderId, trackChanges: false);

                var collaborators = await manager.userFolder.GetCollaboratorsForFolder(folder.BaseFolderId, false);

                if (!collaborators.Where(x => x.Permissions == Permissions.ReadnWrite).Any(x => x.UserId.Equals(user.Id)))
                {
                    throw new UnauthorizedFolderException(folder.BaseFolderId);
                }
            }
            
            var FolderExists = folders.Any(x => x.Name.Equals(folder.Name));

            if (FolderExists)
            {
                throw new Exception("Folder already exists!");
            }
            
            if(folder.BaseFolderId !=  Guid.Empty)
            {
                var baseFolderfromDb = await manager.folder.GetFolder(folder.BaseFolderId, trackChanges: false);
                if (baseFolderfromDb is null)
                {
                    throw new FolderNotFoundException(folder.BaseFolderId);
                }

                var baseFolder = await manager.folder.GetBaseFolder(folder.BaseFolderId, false);

                var collaborators = await manager.userFolder.GetCollaboratorsForFolder(baseFolder.Id, false);

                if (!collaborators.Where(x => x.Permissions == Permissions.ReadnWrite).Any(x => x.UserId.Equals(user.Id)))
                {
                    throw new UnauthorizedFolderException(baseFolder.Id);
                }
            }

            var newFolder = new Entities.Models.Folder
            {
                BaseFolderId = folder.BaseFolderId,
                Name = folder.Name,
                Access = folder.Access,
                CreatedAt = DateTime.Now,
                OwnerId = user?.Id
            };

            manager.folder.CreateFolder(newFolder);

            if (folder.BaseFolderId.Equals(Guid.Empty))
            {
                var userFolder = new UserFolders
                {
                    UserId = user?.Id,
                    FolderId = newFolder.Id,
                    Permissions = Permissions.ReadnWrite
                };

                manager.userFolder.CreateUserFolder(userFolder);
            }
            
            await manager.SaveAsync();
        }

        public async Task DeleteFolderAsync(string username, Guid FolderId)
        {
            var user = await userManager.FindByNameAsync(username);

            var folder = await manager.folder.GetFolder(FolderId, trackChanges: true);

            if(folder is null || folder.OwnerId != user?.Id)
            {
                throw new FolderNotFoundException(FolderId);
            }

            manager.folder.DeleteFolder(folder);
            await manager.SaveAsync();
        }

        public async Task<List<FolderDto>> GetFoldersForUserAsync(string username, RequestParameters parameters)
        {
            var user = await userManager.FindByNameAsync(username);

            var folders = await manager.folder.GetFoldersByUser(user.Id, parameters, trackChanges: false);

            var response = mapper.Map<List<FolderDto>>(folders);

            return response;
        }

        public async Task<FolderV2Dto> GetFolderAsync(string username, Guid Id)
        {
            var user = await userManager.FindByNameAsync(username);

            var folder = await manager.folder.GetFolder(Id, trackChanges: false);

            if (folder is null)
            {
                throw new FolderNotFoundException(Id);
            }

            Entities.Models.Folder baseFolder = null;

            if(folder.BaseFolderId != Guid.Empty)
            {
                baseFolder = await manager.folder.GetBaseFolder(folder.Id, trackChanges: false);
            }

            var permissions = await manager.userFolder.GetCollaboratorsForFolder(baseFolder is null ? Id : baseFolder.Id, false);

            var collaborators = permissions.Select(x => x.UserId)
                .ToList();

            if(folder.Access == Access.Private && collaborators.Any(x => x.Equals(user?.Id)))
            {
                throw new UnauthorizedFolderException(Id);
            }

            var response = new FolderV2Dto
            {
                Id = Id,
                BaseFolderId = baseFolder is null ? Guid.Empty : baseFolder.Id,
                Owner = folder?.Owner?.UserName,
                Name = folder?.Name,
                Access = folder?.Access.ToString(),
                Collaborators = permissions.Select(x => new CollaboratorDto { UserName = x.User.UserName, Permissions = x.Permissions.ToString() }).ToList(),
                Folders = mapper.Map<List<FolderDto>>(await manager.folder.GetChildFolders(Id, trackChanges: false)),
                Contents = null,
                CreatedAt = folder.CreatedAt,
                UpdatedAt = folder.UpdatedAt                
            };

            return response;
        }

        public async Task UpdateFolderAsync(string username, Guid Id, CreateFolderDto update)
        {
            var user = await userManager.FindByNameAsync(username);

            var folder = await manager.folder.GetFolder(Id, trackChanges: true);

            if (folder is null)
            {
                throw new FolderNotFoundException(Id);
            }

            var collaborators = await manager.userFolder.GetCollaboratorsForFolder(Id, false);

            if(collaborators.Where(x => x.Permissions == Permissions.ReadnWrite).Any(x => x.UserId.Equals(user.Id))){
                folder.Name = update.Name;
                folder.Access = update.Access;

                await manager.SaveAsync();
            }
            else
            {
                throw new UnauthorizedFolderException(Id);
            }
            
        }
    }
}
