using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DiscosWebSdk.Clients;
using DiscosWebSdk.Extensions;
using DiscosWebSdk.Interfaces.Clients;
using DiscosWebSdk.Models.ResponseModels;
using DiscosWebSdk.Models.ResponseModels.Entities;
using DiscosWebSdk.Tests.TestDataGenerators;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Shouldly;
using Xunit;

namespace DiscosWebSdk.Tests.Clients;

public class DiscosClientTests
{
	private readonly string       _apiBase = Environment.GetEnvironmentVariable("DISCOS_API_URL") ?? "https://discosweb.esoc.esa.int/api/";
	private readonly IDiscosClient _discosClient;

	private static readonly List<TimeSpan> RetrySpans = new[] {1, 2, 5, 10, 30, 60, 60, 60}.Select(i => TimeSpan.FromSeconds(i)).ToList();
	private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = HttpPolicyExtensions.HandleTransientHttpError().OrResult(res => res.StatusCode is HttpStatusCode.TooManyRequests).WaitAndRetryAsync(RetrySpans);

	public DiscosClientTests()
	{
		HttpClient innerClient = new(new PollyRetryHandler(RetryPolicy, new HttpClientHandler()));
		
		
		innerClient.BaseAddress                         = new(_apiBase);
		innerClient.DefaultRequestHeaders.Authorization = new("bearer", Environment.GetEnvironmentVariable("DISCOS_API_KEY"));
		_discosClient                                   = new DiscosClient(innerClient);
	}

	[Theory]
	[ClassData(typeof(DiscosModelTypesTestDataGenerator))]
	public async Task CanGetSingleOfEveryTypeWithoutQueryParamsGeneric(Type objectType, string id)
	{
		MethodInfo unconstructedGetSingle = typeof(DiscosClient).GetMethods().Single(m => m.Name == nameof(DiscosClient.GetSingle) && m.IsGenericMethod);
		MethodInfo getSingle              = unconstructedGetSingle.MakeGenericMethod(objectType);
		object? res = await getSingle.InvokeAsync(_discosClient, id, string.Empty);

		res.ShouldNotBeNull();
	}
	
	[Theory]
	[ClassData(typeof(DiscosModelTypesTestDataGenerator))]
	public async Task CanGetSingleOfEveryTypeWithoutQueryParamsNonGeneric(Type objectType, string id)
	{
		DiscosModelBase? res = await _discosClient.GetSingle(objectType, id);
		res.ShouldNotBeNull();
	}


	[Theory]
	[ClassData(typeof(DiscosModelTypesTestDataGenerator))]
	public async Task CanGetMultipleOfEveryTypeWithoutQueryParamsGeneric(Type objectType, string _)
	{
		MethodInfo unconstructedGetSingle = typeof(DiscosClient).GetMethods().Single(m => m.Name == nameof(DiscosClient.GetMultiple) && m.IsGenericMethod);
		MethodInfo getMultiple              = unconstructedGetSingle.MakeGenericMethod(objectType);

		IReadOnlyList<DiscosModelBase?>? result;
		if (objectType == typeof(Country)) // No countries on first page of entities...
		{
			result = (IReadOnlyList<DiscosModelBase?>?)await getMultiple.InvokeAsync(_discosClient, "?filter=contains(name,'United')");
		}
		else 
		{
			result = (IReadOnlyList<DiscosModelBase?>?)await getMultiple.InvokeAsync(_discosClient, string.Empty);
		}
		result.ShouldNotBeNull();
		result.Count.ShouldBeGreaterThan(1);
		result.ShouldAllBe(r => r.GetType().IsAssignableTo(typeof(DiscosModelBase)));
	}
	
	
	[Theory]
	[ClassData(typeof(DiscosModelTypesTestDataGenerator))]
	public async Task CanGetMultipleOfEveryTypeWithPaginationWithoutQueryParamsGeneric(Type objectType, string _)
	{
		MethodInfo unconstructedGetSingle = typeof(DiscosClient).GetMethods().Single(m => m.Name == nameof(DiscosClient.GetMultipleWithPaginationState) && m.IsGenericMethod);
		MethodInfo getMultiple              = unconstructedGetSingle.MakeGenericMethod(objectType);

		ModelsWithPagination result;
		if (objectType == typeof(Country)) // No countries on first page of entities...
		{
			result = (ModelsWithPagination)(await getMultiple.InvokeAsync(_discosClient, "?filter=contains(name,'United')"))!;
		}
		else 
		{
			result = (ModelsWithPagination)(await getMultiple.InvokeAsync(_discosClient, string.Empty))!;
		}
		
		result.Models.ShouldNotBeNull();
		result.Models.Count.ShouldBeGreaterThan(1);
		result.Models.ShouldAllBe(r => r.GetType().IsAssignableTo(typeof(DiscosModelBase)));
		
		result.PaginationDetails.CurrentPage.ShouldBe(1);
		result.PaginationDetails.PageSize.ShouldBeGreaterThan(1
															  );
		result.PaginationDetails.TotalPages.ShouldBeGreaterThan(0);
	}
	
	[Theory]
	[ClassData(typeof(DiscosModelTypesTestDataGenerator))]
	public async Task CanGetMultipleOfEveryTypeWithoutQueryParamsNonGeneric(Type objectType, string _)
	{
		IReadOnlyList<DiscosModelBase?>? res; 
		if (objectType == typeof(Country)) // No countries on first page of entities...
		{
			res    = await _discosClient.GetMultiple(objectType, "?filter=contains(name,'United')");
		}
		else 
		{
			res = await _discosClient.GetMultiple(objectType);
		}
		res.ShouldNotBeNull();
		res.Count.ShouldBeGreaterThan(1);
		res.ShouldAllBe(r => r.GetType().IsAssignableTo(typeof(DiscosModelBase)));
	}
	
	[Theory]
	[ClassData(typeof(DiscosModelTypesTestDataGenerator))]
	public async Task CanGetMultipleOfEveryTypeWithPaginationWithoutQueryParamsNonGeneric(Type objectType, string _)
	{
		ModelsWithPagination res; 
		if (objectType == typeof(Country)) // No countries on first page of entities...
		{
			res = await _discosClient.GetMultipleWithPaginationState(objectType, "?filter=contains(name,'United')");
		}
		else 
		{
			res = await _discosClient.GetMultipleWithPaginationState(objectType);
		}
		res.Models.ShouldNotBeNull();
		res.Models.Count.ShouldBeGreaterThan(1);
		res.Models.ShouldAllBe(r => r.GetType().IsAssignableTo(typeof(DiscosModelBase)));
		
		res.PaginationDetails.CurrentPage.ShouldBe(1);
		res.PaginationDetails.PageSize.ShouldBeGreaterThan(1);
		res.PaginationDetails.TotalPages.ShouldBeGreaterThan(0);
	}


	private class PollyRetryHandler : DelegatingHandler
	{
		private readonly AsyncRetryPolicy<HttpResponseMessage> _policy;

		public PollyRetryHandler(AsyncRetryPolicy<HttpResponseMessage> policy, HttpMessageHandler innerHandler)
			: base(innerHandler)
		{
			_policy = policy;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => await _policy.ExecuteAsync(() => base.SendAsync(request, cancellationToken));
	}
}
