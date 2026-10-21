using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;

namespace Supabase.Postgrest.Requests
{
    /// <summary>
    /// Represents a Request that is backed by a caching strategy.
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    public class CacheBackedRequest<TModel> : INotifyPropertyChanged where TModel : BaseModel, new()
    {
        /// <summary>
        /// Handler for when Remote Models have been populated
        /// </summary>
        public delegate void RemoteModelsPopulatedEventHandler(CacheBackedRequest<TModel> sender);

        /// <summary>
        /// The Async action that represents the Remote Request
        /// </summary>
        private readonly Func<Task<ModeledResponse<TModel>>> _remoteRequestAction;

        /// <summary>
        /// The Postgrest Table Instance
        /// </summary>
        private readonly IPostgrestTableWithCache<TModel> _instance;

        /// <summary>
        /// The Cache lookup key - a Base64 encoded reproducible URL for this request configuration.
        /// </summary>
        private string CacheKey { get; }
        
        /// <summary>
        /// The Caching provider.
        /// </summary>
        private readonly IPostgrestCacheProvider _cacheProvider;

        private List<TModel> _models = new List<TModel>();
        private ModeledResponse<TModel>? _response;
        private bool _wasCacheHit;
        private DateTime? _cacheTime;
        private bool _wasResponseCached;

        /// <summary>
        /// The Models returned either by Cache Hit or Remote Response
        /// </summary>
        public List<TModel> Models
        {
            get => _models;
            set => SetField(ref _models, value);
        }

        /// <summary>
        /// The response (if applicable) from <see cref="_remoteRequestAction"/>
        /// </summary>
        public ModeledResponse<TModel>? Response
        {
            get => _response;
            protected set => SetField(ref _response, value);
        }

        /// <summary>
        /// If the cache was hit for this request.
        /// </summary>
        public bool WasCacheHit
        {
            get => _wasCacheHit;
            protected set => SetField(ref _wasCacheHit, value);
        }

        /// <summary>
        /// If the response was stored in cache.
        /// </summary>
        public bool WasResponseCached
        {
            get => _wasResponseCached;
            protected set => SetField(ref _wasResponseCached, value);
        }

        /// <summary>
        /// The stored cache time in UTC.
        /// </summary>
        public DateTime? CacheTime
        {
            get => _cacheTime;
            protected set => SetField(ref _cacheTime, value);
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Invoked when Remote Models have been populated on this object.
        /// </summary>
        public event RemoteModelsPopulatedEventHandler? RemoteModelsPopulated;

        /// <summary>
        /// Constructs a Cache Backed Request that automatically populates itself using the Cache provider (if possible).
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="cacheProvider"></param>
        /// <param name="remoteRequestAction"></param>
        public CacheBackedRequest(IPostgrestTableWithCache<TModel> instance, IPostgrestCacheProvider cacheProvider,
            Func<Task<ModeledResponse<TModel>>> remoteRequestAction)
        {
            _instance = instance;
            _cacheProvider = cacheProvider;
            _remoteRequestAction = remoteRequestAction;

            CacheKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(_instance.GenerateUrl()));
        }

        /// <summary>
        /// Attempts to load a model from the cache. 
        /// </summary>
        internal async Task TryLoadFromCache()
        {
            try
            {
                var cachedModel = await _cacheProvider.GetItem<CachedModel<TModel>?>(CacheKey);

                if (cachedModel == null) return;

                Debugger.Instance.Log(this, $"Loaded cached model from key: {CacheKey}");

                WasCacheHit = true;
                CacheTime = cachedModel.CachedAt;
                Models = cachedModel.Models ?? new List<TModel>();
            }
            catch (Exception ex)
            {
                Debugger.Instance.Log(this, ex.Message);
            }
        }

        /// <summary>
        /// Loads the remote models in the background. Nothing awaits this, so a failure is logged instead of thrown.
        /// </summary>
        internal async Task InvokeAsync()
        {
            try
            {
                await this.LoadRemoteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debugger.Instance.Log(this, ex.Message, ex as PostgrestException);
            }
        }

        /// <summary>
        /// Invokes the stored <see cref="_remoteRequestAction"/>
        /// </summary>
        private async Task LoadRemoteAsync()
        {
            var result = await _remoteRequestAction.Invoke();
            await Cache(result);

            RemoteModelsPopulated?.Invoke(this);
        }

        /// <summary>
        /// Caches a modeled response using the <see cref="_cacheProvider"/>
        /// </summary>
        /// <param name="response"></param>
        private async Task Cache(ModeledResponse<TModel> response)
        {
            var cacheTime = DateTime.UtcNow;
            var modelToBeCached = new CachedModel<TModel>
            {
                Models = response.Models,
                CachedAt = cacheTime
            };

            await _cacheProvider.SetItem(CacheKey, modelToBeCached);

            Response = response;
            CacheTime = cacheTime;
            WasResponseCached = true;
        }

        /// <summary>
        /// Raises a property change event.
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a field within this instance and raises <see cref="OnPropertyChanged"/>
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}