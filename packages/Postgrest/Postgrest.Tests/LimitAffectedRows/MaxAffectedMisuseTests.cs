using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postgrest.Tests.Models;
using Supabase.Postgrest;

namespace Postgrest.Tests.LimitAffectedRows;

/// <summary>
///     <c>MaxAffected</c> is a guard on mutations: a negative limit, or a terminal call that is not an Update or a
///     Delete, is refused before any request so the guard is never silently ignored.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class MaxAffectedMisuseTests
{
    private readonly Client client = new("http://localhost:54321/rest/v1");

    [TestMethod]
    public void MaxAffected_ShouldThrow_GivenANegativeLimit()
    {
        var act = () => this.client.Table<Todo>().MaxAffected(-1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*cannot be negative*");
    }

    [TestMethod]
    public async Task Get_ShouldThrow_GivenMaxAffected()
    {
        var act = () => this.client.Table<Todo>().MaxAffected(1).Get();
        await act.Should().ThrowAsync<InvalidOperationException>("a read cannot honour a mutation limit").WithMessage("*Update and Delete*");
    }

    [TestMethod]
    public async Task Insert_ShouldThrow_GivenMaxAffected()
    {
        var act = () => this.client.Table<Todo>().MaxAffected(1).Insert(new Todo());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task Upsert_ShouldThrow_GivenMaxAffected()
    {
        var act = () => this.client.Table<Todo>().MaxAffected(1).Upsert(new List<Todo> { new() });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task Count_ShouldThrow_GivenMaxAffected()
    {
        var act = () => this.client.Table<Todo>().MaxAffected(1).Count(Constants.CountType.Exact);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
