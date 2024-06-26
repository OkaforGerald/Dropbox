﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contracts;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repository.Extensions;
using SharedAPI.RequestFeatures;

namespace Repository
{
    public class FolderRepository : RepositoryBase<Folder>, IFolderRepository
    {
        public FolderRepository(RepositoryContext context) : base(context)
        {
        }

        public void CreateFolder(Folder Folder)
        {
            Create(Folder);
        }

        public async Task DeleteFolder(Folder Folder)
        {
            Delete(Folder);

            var children = await GetAllSubFolders(Folder.OwnerId, Folder.Id, new List<Folder> { }, true);

            DeleteMultiple(children);
        }

        public async Task<Folder> GetFolder(Guid FolderId, bool trackChanges)
        {
            return await FindByCondition(x => x.Id == FolderId, trackChanges)
                .Include(x => x.Owner)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Folder>> GetFoldersByUser(string UserId, RequestParameters parameters, bool trackChanges)
        {
            return await FindByCondition(x => x.OwnerId.Equals(UserId) && x.BaseFolderId.Equals(Guid.Empty), trackChanges)
                .Include(x => x.Owner)
                .Filter(parameters.FolderType)
                .Search(parameters.SearchTerm)
                .Sort<Folder>(parameters.OrderBy)
                .ToListAsync();
        }

        public async Task<List<Folder>> GetFoldersByUser(string UserId, bool trackChanges)
        {
            return await FindByCondition(x => x.OwnerId.Equals(UserId) && x.BaseFolderId.Equals(Guid.Empty), trackChanges)
                .ToListAsync();
        }

        public async Task<List<Folder>> GetChildFolders(Guid Id, string OwnerId, bool trackChanges)
        {
            return await FindByCondition(x => x.BaseFolderId.Equals(Id) && x.OwnerId.Equals(OwnerId), trackChanges)
                .ToListAsync();
        }

        public async Task<List<Folder>> GetChildFolders(Guid Id, string OwnerId, RequestParameters parameters, bool trackChanges)
        {
            return await FindByCondition(x => x.BaseFolderId.Equals(Id) && x.OwnerId.Equals(OwnerId), trackChanges)
                .Search(parameters.SearchTerm)
                .ToListAsync();
        }

        public async Task<Folder> GetBaseFolder(Guid Id, bool trackChanges)
        {
            Folder baseFolder = await GetFolder(Id, trackChanges);

            while(baseFolder.BaseFolderId != Guid.Empty)
            {
                baseFolder = await GetFolder(baseFolder.BaseFolderId, trackChanges);
            }

            return baseFolder;
        }

        public async Task<Folder> GetFolderByName(string OwnerId, string FolderName, bool trackChanges)
        {
            return await FindByCondition(x => x.OwnerId.Equals(OwnerId) && x.Name.Equals(FolderName) && x.BaseFolderId.Equals(Guid.Empty), trackChanges)
                .FirstOrDefaultAsync();
        }

        public async Task<Folder> GetFolderByPath(string OwnerId, string AbsolutePath, bool trackChanges)
        {
            return await FindByCondition(x => x.OwnerId.Equals(OwnerId) && x.PathOnLocal.Equals(AbsolutePath) && x.IsOnLocal, trackChanges)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Folder>> GetAllSubFolders(string ownerId, Guid FolderId, List<Folder> Folders, bool trackChanges)
        {
            var children = await GetChildFolders(FolderId, ownerId, trackChanges);
            if (!children.Any())
            {
                return Folders;
            }
            else
            {
                foreach (var folder in children)
                {
                    Folders.Add(folder);

                    await GetAllSubFolders(ownerId, folder.Id, Folders, trackChanges);
                }

                return Folders;
            }
        }
    }
}
