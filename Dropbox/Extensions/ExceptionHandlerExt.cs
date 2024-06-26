﻿using Entities.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using SharedAPI;

namespace Dropbox.Extensions
{
    public static class ExceptionHandlerExt
    {
        public static void ConfigureExceptionHandler(this WebApplication application)
        {
            application.UseExceptionHandler(
                Error =>
                {
                    Error.Run(async context =>
                    {
                        context.Response.ContentType = "application/json";

                        var contextFeatures = context.Features.Get<IExceptionHandlerFeature>();

                        if(contextFeatures != null)
                        {
                            context.Response.StatusCode = contextFeatures.Error switch
                            {
                                NotFoundException => StatusCodes.Status404NotFound,
                                UnauthorizedFolderException => StatusCodes.Status401Unauthorized,
                                UnauthorizedAction => StatusCodes.Status401Unauthorized,
                                _ => StatusCodes.Status500InternalServerError
                            };

                            await context.Response.WriteAsync(new ResponseDto<string>
                            {
                                StatusCode = context.Response.StatusCode,
                                Errors = new List<string> { contextFeatures.Error.Message }
                            }.ToString());
                        }
                    });
                });
        }
    }
}
