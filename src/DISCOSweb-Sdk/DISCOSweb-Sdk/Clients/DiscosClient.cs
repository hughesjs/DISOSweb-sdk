using System.Net;
using System.Net.Http.Headers;
using DISCOSweb_Sdk.Mapping.JsonApi;
using DISCOSweb_Sdk.Models.ResponseModels.DiscosObjects;
using DISCOSweb_Sdk.Models.ResponseModels.Entities;
using DISCOSweb_Sdk.Models.ResponseModels.FragmentationEvent;
using DISCOSweb_Sdk.Models.ResponseModels.Launches;
using DISCOSweb_Sdk.Models.ResponseModels.LaunchVehicles;
using DISCOSweb_Sdk.Models.ResponseModels.Orbits;
using DISCOSweb_Sdk.Models.ResponseModels.Propellants;
using DISCOSweb_Sdk.Models.ResponseModels.Reentries;
using DISCOSweb_Sdk.Options;
using Hypermedia.JsonApi.Client;
using Microsoft.Extensions.Options;

namespace DISCOSweb_Sdk.Clients;

public class DiscosClient : IDiscosClient
{
	private readonly HttpClient _client;

	public DiscosClient(HttpClient client)
	{
		_client = client;
	}


	private readonly Dictionary<Type, string> _endpoints = new()
														   {
															   {typeof(DiscosObject), "objects"},
															   {typeof(DiscosObjectClass), "object-classes"},
															   {typeof(Country), "entities"},
															   {typeof(Organisation), "entities"},
															   {typeof(Entity), "entities"},
															   {typeof(FragmentationEvent), "fragmentations"},
															   {typeof(Launch), "launches"},
															   {typeof(LaunchSite), "launch-sites"},
															   {typeof(LaunchSystem), "launch-systems"},
															   {typeof(LaunchVehicle), "launch-vehicles"},
															   {typeof(LaunchVehicleFamily), "launch-vehicles/families"},
															   {typeof(LaunchVehicleEngine), "launch-vehicles/engines"},
															   {typeof(LaunchVehicleStage), "launch-vehicles/stages"},
															   {typeof(InitialOrbitDetails), "initial-orbits"},
															   {typeof(DestinationOrbitDetails), "destination-orbits"},
															   {typeof(Propellant), "propellants"},
															   {typeof(Reentry), "reentries"}
														   };

	public async Task<T> GetSingle<T>(string id, string queryString = "")
	{
		string              endpoint = _endpoints[typeof(T)];
		HttpResponseMessage res      = await GetWithRateLimitRetry($"{endpoint}/{id}{queryString}");
		return await res.Content.ReadAsJsonApiAsync<T>(DiscosObjectResolver.CreateResolver());
	}

	public async Task<IReadOnlyList<T>> GetMultiple<T>(string queryString = "")
	{
		string              endpoint = _endpoints[typeof(T)];
		HttpResponseMessage res      = await GetWithRateLimitRetry($"{endpoint}{queryString}");
		return await res.Content.ReadAsJsonApiManyAsync<T>(DiscosObjectResolver.CreateResolver());
	}

	private async Task<HttpResponseMessage> GetWithRateLimitRetry(string uri, int retries = 0)
	{
		const int           maxAttempts = 5;
		HttpResponseMessage res         = await _client.GetAsync(uri);
		if (res.StatusCode == HttpStatusCode.TooManyRequests)
		{
			if (retries >= maxAttempts) return res;
			RetryConditionHeaderValue? retryAfter = res.Headers.RetryAfter;
			Console.WriteLine($"Hit rate limit. Waiting for {retryAfter.Delta.Value.TotalSeconds}s. Retry Number: {retries}");
			await Task.Delay(retryAfter.Delta.Value);
			return await GetWithRateLimitRetry(uri, ++retries);
		}
		if (res.StatusCode == HttpStatusCode.BadGateway)
		{
			if (retries >= maxAttempts) return res;
			Console.WriteLine("Bad Gateway, probably transient. Waiting for 5s...");
			await Task.Delay(TimeSpan.FromSeconds(5));
			return await GetWithRateLimitRetry(uri, ++retries);
		}
		res.EnsureSuccessStatusCode();
		return res;
	}
}


public interface IDiscosClient
{
	public Task<T>                GetSingle<T>(string   id, string queryString = "");
	public Task<IReadOnlyList<T>> GetMultiple<T>(string queryString = "");
}
