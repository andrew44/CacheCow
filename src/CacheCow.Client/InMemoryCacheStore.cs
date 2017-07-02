﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Text;
using CacheCow.Common;
using System.Threading.Tasks;

namespace CacheCow.Client
{
	public class InMemoryCacheStore : ICacheStore
	{
        private const string CacheStoreEntryName = "###InMemoryCacheStore_###";
	    private static TimeSpan DefaultCacheExpiry = TimeSpan.FromHours(6);

        private MemoryCache _responseCache = new MemoryCache(CacheStoreEntryName);
		private MessageContentHttpMessageSerializer _messageSerializer = new MessageContentHttpMessageSerializer(true);
	    private readonly TimeSpan _defaultExpiry;

	    public InMemoryCacheStore()
            : this(DefaultCacheExpiry)
        {
            
        }

        public InMemoryCacheStore(TimeSpan defaultExpiry)
        {
            _defaultExpiry = defaultExpiry;
        }

        private DateTimeOffset GetExpiry(HttpResponseMessage response)
        {      
            if (response.Headers.CacheControl != null && response.Headers.CacheControl.MaxAge.HasValue)
            {
                return DateTimeOffset.UtcNow.Add(response.Headers.CacheControl.MaxAge.Value);
            }
           
            return response.Content != null && response.Content.Headers.Expires.HasValue
                        ? response.Content.Headers.Expires.Value
                        : DateTimeOffset.UtcNow.Add(_defaultExpiry);
        }

	    public void Dispose()
	    {
	        _responseCache.Dispose();
	    }

	    public async Task<HttpResponseMessage> GetValueAsync(CacheKey key)
	    {
            var result = _responseCache.Get(key.HashBase64);
	        if (result == null)
	            return null;

	        return (await _messageSerializer.DeserializeToResponseAsync(new MemoryStream((byte[]) result)));
	    }

	    public async Task AddOrUpdateAsync(CacheKey key, HttpResponseMessage response)
	    {
            // removing reference to request so that the request can get GCed
            var req = response.RequestMessage;
            response.RequestMessage = null;
            var memoryStream = new MemoryStream();
	        await _messageSerializer.SerializeAsync(response, memoryStream);
            response.RequestMessage = req;
            _responseCache.Set(key.HashBase64, memoryStream.ToArray(), GetExpiry(response));
	    }

	    public Task<bool> TryRemoveAsync(CacheKey key)
	    {
            return Task.FromResult(_responseCache.Remove(key.HashBase64) != null);
	    }

	    public Task ClearAsync()
	    {
            _responseCache.Dispose();
            _responseCache = new MemoryCache(CacheStoreEntryName);
	        return Task.FromResult(0);
	    }
	}
}
