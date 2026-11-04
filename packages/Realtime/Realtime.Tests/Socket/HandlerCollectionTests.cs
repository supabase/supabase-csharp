using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;

namespace Realtime.Tests.Socket;

[TestClass]
[TestCategory("Unit")]
public class HandlerCollectionTests
{
    private delegate void Handler(int value);

    private static void NoOp(int value) { }
    private static void Other(int value) { }

    [TestMethod]
    public void Add_ShouldKeepOneEntry_GivenTheSameHandlerTwice()
    {
        var handlers = new HandlerCollection<Handler>();
        var handler = new Handler(NoOp);

        handlers.Add(handler);
        handlers.Add(handler);

        handlers.Snapshot().Should().ContainSingle("a handler is registered at most once");
    }

    [TestMethod]
    public void Add_ShouldKeepRegistrationOrder_GivenSeveralHandlers()
    {
        var handlers = new HandlerCollection<Handler>();
        var first = new Handler(NoOp);
        var second = new Handler(Other);

        handlers.Add(first);
        handlers.Add(second);

        handlers.Snapshot().Should().Equal(new[] { first, second }, "handlers are invoked in registration order");
    }

    [TestMethod]
    public void Remove_ShouldDoNothing_GivenAHandlerThatWasNeverAdded()
    {
        var handlers = new HandlerCollection<Handler>();
        var kept = new Handler(NoOp);
        handlers.Add(kept);

        handlers.Remove(new Handler(Other));

        handlers.Snapshot().Should().Equal(new[] { kept }, "removing an unknown handler leaves the others alone");
    }

    [TestMethod]
    public void Remove_ShouldDropTheHandler_GivenARegisteredHandler()
    {
        var handlers = new HandlerCollection<Handler>();
        var handler = new Handler(NoOp);
        handlers.Add(handler);

        handlers.Remove(handler);

        handlers.Snapshot().Should().BeEmpty();
    }

    [TestMethod]
    public void Clear_ShouldEmptyTheCollection()
    {
        var handlers = new HandlerCollection<Handler>();
        handlers.Add(new Handler(NoOp));
        handlers.Add(new Handler(Other));

        handlers.Clear();

        handlers.Snapshot().Should().BeEmpty();
    }

    [TestMethod]
    public void Snapshot_ShouldNotChange_GivenLaterModifications()
    {
        var handlers = new HandlerCollection<Handler>();
        var original = new Handler(NoOp);
        handlers.Add(original);

        var snapshot = handlers.Snapshot();
        handlers.Add(new Handler(Other));
        handlers.Remove(original);
        handlers.Clear();

        snapshot.Should().Equal(new[] { original }, "a snapshot is a copy, not a live view");
    }

    [TestMethod]
    public void Dispatch_ShouldFinish_GivenAHandlerThatRemovesItselfMidDispatch()
    {
        var handlers = new HandlerCollection<Handler>();
        var calls = new List<string>();
        Handler? selfRemoving = null;
        selfRemoving = _ =>
        {
            calls.Add("self");
            handlers.Remove(selfRemoving!);
        };
        Handler next = _ => calls.Add("next");
        handlers.Add(selfRemoving);
        handlers.Add(next);

        foreach (var handler in handlers.Snapshot())
            handler(1);

        calls.Should().Equal("self", "next");
        handlers.Snapshot().Should().Equal(new[] { next }, "the self-removing handler is gone for later dispatches");
    }

    [TestMethod]
    public void Dispatch_ShouldStillReachEveryHandler_GivenAHandlerThatClearsTheCollection()
    {
        var handlers = new HandlerCollection<Handler>();
        var calls = new List<string>();
        handlers.Add(_ =>
        {
            calls.Add("clearing");
            handlers.Clear();
        });
        handlers.Add(_ => calls.Add("second"));

        foreach (var handler in handlers.Snapshot())
            handler(1);

        calls.Should().Equal("clearing", "second");
        handlers.Snapshot().Should().BeEmpty();
    }
}
