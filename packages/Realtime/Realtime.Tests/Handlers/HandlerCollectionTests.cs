using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;

namespace Realtime.Tests.Handlers;

/// <summary>
///     Every handler list in the package is a <see cref="HandlerCollection{THandler}" />: handlers are unique,
///     kept in registration order, and dispatched from a snapshot that later changes do not affect.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public class HandlerCollectionTests
{
    private delegate void Handler(int value);

    private readonly HandlerCollection<Handler> handlers = new();
    private readonly List<string> calls = new();

    private static void NoOp(int value) { }
    private static void Other(int value) { }

    [TestMethod]
    public void Add_ShouldKeepOneEntry_GivenTheSameHandlerTwice()
    {
        var handler = new Handler(NoOp);

        this.handlers.Add(handler);
        this.handlers.Add(handler);

        this.handlers.Snapshot().Should().ContainSingle("a handler is registered at most once");
    }

    [TestMethod]
    public void Add_ShouldKeepRegistrationOrder_GivenSeveralHandlers()
    {
        var first = new Handler(NoOp);
        var second = new Handler(Other);

        this.handlers.Add(first);
        this.handlers.Add(second);

        this.handlers.Snapshot().Should().Equal(new[] { first, second }, "handlers are invoked in registration order");
    }

    [TestMethod]
    public void Remove_ShouldDoNothing_GivenAHandlerThatWasNeverAdded()
    {
        var kept = new Handler(NoOp);
        this.handlers.Add(kept);

        this.handlers.Remove(new Handler(Other));

        this.handlers.Snapshot().Should().Equal(new[] { kept }, "removing an unknown handler leaves the others alone");
    }

    [TestMethod]
    public void Remove_ShouldDropTheHandler_GivenARegisteredHandler()
    {
        var handler = new Handler(NoOp);
        this.handlers.Add(handler);

        this.handlers.Remove(handler);

        this.handlers.Snapshot().Should().BeEmpty("a removed handler is no longer dispatched to");
    }

    [TestMethod]
    public void Remove_ShouldDropTheHandler_GivenItRemovesItselfMidDispatch()
    {
        var next = new Handler(_ => this.calls.Add("next"));
        this.handlers.Add(this.SelfRemovingHandler());
        this.handlers.Add(next);

        this.Dispatch();

        this.handlers.Snapshot().Should().Equal(new[] { next }, "the self-removing handler is gone for later dispatches");
    }

    [TestMethod]
    public void Clear_ShouldEmptyTheCollection()
    {
        this.handlers.Add(new Handler(NoOp));
        this.handlers.Add(new Handler(Other));

        this.handlers.Clear();

        this.handlers.Snapshot().Should().BeEmpty("clearing drops every handler");
    }

    [TestMethod]
    public void Snapshot_ShouldNotChange_GivenLaterModifications()
    {
        var original = new Handler(NoOp);
        this.handlers.Add(original);

        var snapshot = this.handlers.Snapshot();
        this.handlers.Add(new Handler(Other));
        this.handlers.Remove(original);
        this.handlers.Clear();

        snapshot.Should().Equal(new[] { original }, "a snapshot is a copy, not a live view");
    }

    [TestMethod]
    public void Snapshot_ShouldReachEveryHandler_GivenAHandlerThatRemovesItself()
    {
        this.handlers.Add(this.SelfRemovingHandler());
        this.handlers.Add(_ => this.calls.Add("next"));

        this.Dispatch();

        this.calls.Should().Equal(new[] { "self", "next" }, "removing itself does not cut the dispatch short");
    }

    [TestMethod]
    public void Snapshot_ShouldReachEveryHandler_GivenAHandlerThatClearsTheCollection()
    {
        this.handlers.Add(_ =>
        {
            this.calls.Add("clearing");
            this.handlers.Clear();
        });
        this.handlers.Add(_ => this.calls.Add("second"));

        this.Dispatch();

        this.calls.Should().Equal(new[] { "clearing", "second" }, "clearing does not cut the dispatch short");
    }

    private Handler SelfRemovingHandler()
    {
        Handler? selfRemoving = null;
        selfRemoving = _ =>
        {
            this.calls.Add("self");
            this.handlers.Remove(selfRemoving!);
        };
        return selfRemoving;
    }

    private void Dispatch()
    {
        foreach (var handler in this.handlers.Snapshot())
            handler(1);
    }
}
