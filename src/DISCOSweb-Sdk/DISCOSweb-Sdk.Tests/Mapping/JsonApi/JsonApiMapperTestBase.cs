using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DISCOSweb_Sdk.Mapping.JsonApi;
using DISCOSweb_Sdk.Models.ResponseModels.DiscosObjects;
using Hypermedia.JsonApi.Client;

namespace DISCOSweb_Sdk.Tests.Mapping.JsonApi;

public abstract class JsonApiMapperTestBase
{
	private const string ApiBase = "https://discosweb.esoc.esa.int/api/";

	private readonly Dictionary<Type, string> _endpoints = new()
														  {
															  {typeof(DiscosObject), "objects" }
														  };

	protected async Task<T> FetchSingle<T>(string id, string queryString = "")
	{
		HttpClient client = new();
		client.DefaultRequestHeaders.Authorization = new("bearer", Environment.GetEnvironmentVariable("DISCOS_API_KEY"));
		string endpoint = _endpoints[typeof(T)];
		HttpResponseMessage res = await client.GetAsync($"{ApiBase}{endpoint}/{id}{queryString}");
		res.EnsureSuccessStatusCode();
		return await res.Content.ReadAsJsonApiAsync<T>(DiscosObjectResolver.CreateResolver());
	}

	protected async Task<IReadOnlyList<T>> FetchMultiple<T>(string queryString = "")
	{
		HttpClient client = new();
		client.DefaultRequestHeaders.Authorization = new("bearer", Environment.GetEnvironmentVariable("DISCOS_API_KEY"));
		HttpResponseMessage res = await client.GetAsync($"https://discosweb.esoc.esa.int/api/objects{queryString}");
		res.EnsureSuccessStatusCode();
		return await res.Content.ReadAsJsonApiManyAsync<T>(DiscosObjectResolver.CreateResolver());
	}
}