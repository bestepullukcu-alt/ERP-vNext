using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VP-4F — moving a Route stop to another day: a drop on a day tab (or "Move to day…") is a 4E DAY PIN made by the
/// Weeks component (the same question, rules and session update as WP-VP-4D); the order INSIDE a day stays the manual
/// order; an approved / past week and a holiday / weekend / gone day take no drop; the route opens on the target day;
/// the pin marks, "Remove the pin" and the overflow line are the Weeks component's, drawn on the Route's cards.
/// </summary>
public sealed class VisitPlanningRouteMoveWebTests
{
    [Fact]
    public void A_stop_dropped_on_a_day_tab_asks_the_weeks_component_for_a_day_pin()
    {
        var details = Script("details.js");
        var drop = Between(details, "tabs.addEventListener('drop', e => {", "\n        });");
        Assert.Contains("crossDayMove = true;", drop);
        Assert.Contains("if (draggingVisitCid != null) requestDayMove(visitSlot(draggingVisitCid), 'visit', order);", drop);
        Assert.Contains("else requestDayMove(blockSlot(draggingBlockIdx), 'institution', order);", drop);
        var request = Between(details, "const requestDayMove = (slot, scope, order) => {", "\n    };");
        Assert.Contains("if (!slot || !routeCanMove()) return;", request);
        Assert.Contains("page.emit('request:move-visit', { slot: slot, scope: scope, date: date });", request);
        // the old manual-order approximation is gone
        Assert.DoesNotContain("moveBlockToDay", details);

        // the Weeks component makes the pin: a drop → dropOn (question for a doctor of a larger group), the keyboard →
        // the dialog; both end in the session update with the week's dayPins
        var weeks = Script("weeks.js");
        var handler = Between(weeks, "page.on('request:move-visit', e => {", "\n    });");
        Assert.Contains("if (e.date) dropOn(e.slot, e.scope, e.date);", handler);
        Assert.Contains("else askMove(e.slot, e.scope === 'institution' ? 'institution' : 'visit');", handler);
        Assert.Contains("JSON.stringify({ dayPins: { weekStart: ws, pins: pins }, expectedVersion: session().version })", weeks);
        Assert.Contains("if (scope === 'visit' && members.length > 1) { askMove(slot, 'visit', date); return; }", weeks);
    }

    [Fact]
    public void The_order_inside_a_day_stays_the_manual_order()
    {
        var details = Script("details.js");
        Assert.Contains("const onManualReorder = () => { manualOrder = collectManualOrder(); manualIsUser = true; preview(); };", details);
        // a stop dragged within the day: the manual order (never a day pin)
        var blockSortable = Between(details, "window.Sortable.create(el('vp-visit-cards'), {", "\n        });");
        Assert.Contains("onEnd: () => { const wasCrossDay = crossDayMove; crossDayMove = false; draggingBlockIdx = null; if (!wasCrossDay) onManualReorder(); }", blockSortable);
        Assert.DoesNotContain("request:move-visit", blockSortable);
        Assert.DoesNotContain("requestDayMove", blockSortable);
        // a doctor card dragged within its stop: the manual order too
        var cardSortable = Between(details, "const wireBlockSortable = (idx, detail) => {", "\n    };");
        Assert.Contains("if (wasCrossDay) return;", cardSortable);
        Assert.Contains("onManualReorder();", cardSortable);
        Assert.DoesNotContain("request:move-visit", cardSortable);
        Assert.DoesNotContain("requestDayMove", cardSortable);
        // the manual order still rides on the preview
        Assert.Contains("if (manualOrder && manualOrder.length) body.manualVisitOrder = manualOrder;", details);
    }

    [Fact]
    public void An_approved_or_past_week_and_a_holiday_weekend_or_gone_day_take_no_drop()
    {
        var details = Script("details.js");
        Assert.Contains("const ROUTE_MOVABLE_WEEK_STATUSES = ['draft', 'empty'];", details);
        Assert.Contains("const routeCanMove = () => !!page && canGenerate && !readOnly && !page.isLegacy()", details);
        Assert.Contains("ROUTE_MOVABLE_WEEK_STATUSES.indexOf(page.weekStatus(page.state.weekStart)) > -1", details);
        var accepts = Between(details, "const accepts = t =>", ";\n");
        Assert.Contains("!t.classList.contains('disabled')", accepts);           // a holiday tab is disabled; weekends are not drawn
        Assert.Contains("routeCanMove()", accepts);
        Assert.Contains(">= ymd(new Date())", accepts);                          // never onto a gone day
        Assert.Contains("parseInt(t.dataset.day, 10) !== activeDayOrderVal", accepts);
        Assert.Contains("if (accepts(t)) { e.preventDefault(); hi(t); }", details); // dragover only where a drop is allowed
        Assert.Contains("if (!accepts(t)) return;", Between(details, "tabs.addEventListener('drop', e => {", "\n        });"));
        // the component re-checks (4D rules)
        var weeks = Script("weeks.js");
        Assert.Contains("const DROPPABLE_DAY_KINDS = ['working', 'half'];", weeks);
        Assert.Contains("if (!canMove(w)) return;", Between(weeks, "const dropOn = (slot, scope, date) => {", "\n    };"));
    }

    [Fact]
    public void After_the_move_the_route_opens_on_the_target_day()
    {
        var details = Script("details.js");
        Assert.Contains("page.on('day-pin:moved', e => { if (e && e.date) pendingDayOrder = dayOrder(e.date); });", details);
        Assert.Contains("const wanted = pendingDayOrder != null && enabled.some(d => d.order === pendingDayOrder) ? pendingDayOrder : activeDayOrderVal;", details);
        var weeks = Script("weeks.js");
        Assert.Contains(".then(ok => { if (ok) page.emit('day-pin:moved', { date: date }); });", weeks);
        Assert.Contains("return true;", Between(weeks, "const savePins = (ws, pins) =>", "\n    });"));
    }

    [Fact]
    public void Route_cards_carry_the_weeks_pin_marks_unpin_move_to_day_and_the_overflow_line()
    {
        var details = Script("details.js");
        Assert.Contains("page.emit('route:day-rendered', { date: ymd(dayDate) });", details);
        Assert.Contains("if (e.target.closest('.js-route-pin')) return;", details);

        var weeks = Script("weeks.js");
        var decorate = Between(weeks, "const decorateRoute = date => {", "\n    };");
        Assert.Contains("routeControls(slots, 'institution', !!block.closest('.vp-tl-row--pharmacy'))", decorate);
        Assert.Contains("routeControls(slots, 'visit')", decorate);
        Assert.Contains("note.innerHTML = pinOverflowHtml(preview(), page.state.weekStart);", decorate);
        var controls = Between(weeks, "const routeControls = (slots, scope, lockedStop) => {", "\n    };");
        Assert.Contains("pinMark(pinned)", controls);
        Assert.Contains("js-route-unpin", controls);
        Assert.Contains("js-route-move", controls);
        Assert.Contains("const movable = !lockedStop && canMove(page.week(page.state.weekStart));", controls);
        Assert.Contains("if (move) { askMove(s, move.dataset.scope === 'institution' ? 'institution' : 'visit'); return; }", weeks);
        Assert.Contains("savePins(ws, pinsAfterUnpin(weekPins(ws), s, membersOf(s, weekSlots)));", weeks);
        // the same overflow sentence as the Weeks tab
        Assert.Contains("parts.push(pinOverflowHtml(p, ws));", weeks);
        Assert.Contains("fmt(L.PinOverflowMessage || '{0} {1}', byTarget[k], dayLabel(k))", Between(weeks, "const pinOverflowHtml = (p, ws) => {", "\n    };"));
        Assert.Contains("id=\"vp-route-pin-overflow\"", View("Details.cshtml"));
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, end);
        return text[from..(to + end.Length)];
    }

    private static string Script(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static string View(string file) =>
        File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitPlanning", file)).Replace("\r\n", "\n");

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
