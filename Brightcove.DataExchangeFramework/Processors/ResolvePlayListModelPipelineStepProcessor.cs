﻿using Brightcove.Core.Models;
using Brightcove.Core.Services;
using Brightcove.DataExchangeFramework.Settings;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.DataExchange.Contexts;
using Sitecore.DataExchange.Extensions;
using Sitecore.DataExchange.Models;
using Sitecore.DataExchange.Processors.PipelineSteps;
using Sitecore.Services.Core.Diagnostics;
using Sitecore.Services.Core.Model;
using System;

namespace Brightcove.DataExchangeFramework.Processors
{
    public class ResolvePlaylistModelPipelineStepProcessor : BasePipelineStepWithWebApiEndpointProcessor
    {
        BrightcoveService service;

        protected override void ProcessPipelineStep(PipelineStep pipelineStep = null, PipelineContext pipelineContext = null, ILogger logger = null)
        {
            base.ProcessPipelineStep(pipelineStep, pipelineContext, logger);

            if(pipelineContext.CriticalError)
            {
                return;
            }

            var resolveAssetModelSettings = pipelineStep.GetPlugin<ResolveAssetModelSettings>();
            if (resolveAssetModelSettings == null)
            {
                logger.Error(
                    "No resolve asset model settings are specified for the pipeline step. " +
                    "(pipeline step: {0})",
                    pipelineStep.Name);

                pipelineContext.CriticalError = true;
                return;
            }

            try
            {
                service = new BrightcoveService(WebApiSettings.AccountId, WebApiSettings.ClientId, WebApiSettings.ClientSecret);
                ItemModel item = (ItemModel)pipelineContext.GetObjectFromPipelineContext(resolveAssetModelSettings.AssetItemLocation);
                string playlistId = (string)item["ID"];
                PlayList playlist;

                if (string.IsNullOrWhiteSpace(playlistId))
                {
                    logger.Debug($"Creating brightcove model for the brightcove item '{item.GetItemId()}' (pipeline step: {pipelineStep.Name})");
                    playlist = CreatePlaylist(item);
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, playlist);
                }
                else if (service.TryGetPlaylist(playlistId, out playlist))
                {
                    pipelineContext.SetObjectOnPipelineContext(resolveAssetModelSettings.AssetModelLocation, playlist);
                    logger.Debug($"Resolved the brightcove item '{item.GetItemId()}' to the brightcove model '{playlistId}' (pipeline step: {pipelineStep.Name})");
                }
                else
                {
                    //The item was probably deleted or the ID has been modified incorrectly so we delete the item
                    logger.Warn($"Deleting the brightcove item '{item.GetItemId()}' because the corresponding brightcove model '{playlistId}' could not be found (pipeline step: {pipelineStep.Name})");
                    Sitecore.Context.ContentDatabase.GetItem(item.GetItemId().ToString()).Delete();
                }
            }
            catch(Exception ex)
            {
                logger.Error($"Failed to resolve the brightcove item because an unexpected error has occured (pipeline step: {pipelineStep.Name}, exception: {ex.Message})");
            }
        }

        private PlayList CreatePlaylist(ItemModel itemModel)
        {
            PlayList playlist = service.CreatePlaylist((string)itemModel["Name"]);
            Item item = Sitecore.Context.ContentDatabase.GetItem(new ID(itemModel.GetItemId()));

            item.Editing.BeginEdit();
            item["ID"] = playlist.Id;
            item.Editing.EndEdit();

            return playlist;
        }
    }
}
