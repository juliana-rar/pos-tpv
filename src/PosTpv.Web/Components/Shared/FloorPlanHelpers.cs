using System.Globalization;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Enums;

namespace PosTpv.Web.Components.Shared;

/// <summary>
/// Pure formatting/geometry helpers shared by the /tables page and its <see cref="FloorMap"/>
/// child, so both render tables, zones and decor from the exact same rules.
/// </summary>
internal static class FloorPlanHelpers
{
    // Cooler, darker "corporate upholstery" palette (navy, teal, slate, taupe, aubergine, sage)
    // instead of a warmer/brighter one — used for joined-group badges, so the whole floor plan
    // reads as enterprise software, not a colour wheel.
    internal static readonly string[] GroupColors = { "#2f4a68", "#2f6b66", "#52657a", "#6b5f4a", "#5c4a5c", "#3f5c4a" };

    // Numbers must be invariant so CSS/px values never use a comma decimal separator.
    internal static string N(double d) => d.ToString(CultureInfo.InvariantCulture);

    internal static string GroupColor(int gid) => GroupColors[Math.Abs(gid) % GroupColors.Length];

    internal static string DecorSlug(FloorDecorType type) => type.ToString().ToLowerInvariant();

    internal static string DecorLabel(FloorDecorType type) => type switch
    {
        FloorDecorType.PottedPlant => "Potted plant",
        FloorDecorType.SmallPlant => "Small plant",
        FloorDecorType.HangingPlant => "Hanging plant",
        FloorDecorType.Bush => "Bush",
        FloorDecorType.SmallTree => "Small tree",
        FloorDecorType.Fern => "Fern",
        FloorDecorType.Wall => "Wall",
        FloorDecorType.Door => "Door",
        FloorDecorType.BarCounter => "Bar counter",
        FloorDecorType.Column => "Column",
        FloorDecorType.Window => "Window",
        FloorDecorType.GlassWall => "Glass wall",
        FloorDecorType.Banquette => "Banquette",
        FloorDecorType.Rug => "Rug",
        FloorDecorType.Planter => "Planter",
        FloorDecorType.Parasol => "Parasol",
        FloorDecorType.WineRack => "Wine rack",
        FloorDecorType.Kitchen => "Kitchen",
        FloorDecorType.Restrooms => "Restrooms",
        FloorDecorType.Stairs => "Stairs",
        FloorDecorType.HostStand => "Host stand",
        _ => type.ToString(),
    };

    internal static string DecorStyle(FloorDecorDto d) =>
        $"left:{N(d.PositionX)}px; top:{N(d.PositionY)}px; width:{N(d.Width)}px; height:{N(d.Height)}px; transform:rotate({N(d.Rotation)}deg); --rot:{N(d.Rotation)}deg";

    internal static string ZoneStyle(FloorZoneDto z)
    {
        var s = $"left:{N(z.PositionX)}px; top:{N(z.PositionY)}px; width:{N(z.Width)}px; height:{N(z.Height)}px;";
        if (z.Color is not null)
        {
            if (IsImageColor(z.Color)) s += $" --zimg:url('{z.Color}');";
            else s += $" --zcolor:{z.Color};";
        }
        return s;
    }

    internal static string TableStyle(TableDto t)
    {
        var s = $"left:{N(t.PositionX)}px; top:{N(t.PositionY)}px; width:{N(t.Width)}px; height:{N(t.Height)}px; transform:rotate({N(t.Rotation)}deg); --rot:{N(t.Rotation)}deg";
        if (t.GroupId is not null) s += $"; --grp:{GroupColor(t.GroupId.Value)}";
        if (t.Color is not null)
        {
            if (IsImageColor(t.Color)) { s += $"; --timg:url('{t.Color}'); --tfg:#fff"; }
            else
            {
                s += $"; --tcolor:{t.Color}";
                if (IsDarkColor(t.Color)) s += "; --tfg:#fff";
            }
        }
        return s;
    }

    // Colour values picked via ColorPickerPanel are either a "#rrggbb" hex or an "/uploads/..."
    // image path (see ColorPickerPanel's own Value/IsImage) — this is the same '#'-prefix check
    // used there, kept in sync so a value round-trips as the same kind on both ends.
    internal static bool IsImageColor(string color) => !color.StartsWith('#');

    // Quick perceived-luminance check so a custom/dark accent colour (picked freely via the
    // native colour input, unlike the fixed GroupColors palette) automatically gets white
    // labels instead of the default dark text becoming unreadable on it.
    internal static bool IsDarkColor(string hex)
    {
        if (hex.Length < 7) return false;
        if (!byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, null, out var r)) return false;
        if (!byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, null, out var g)) return false;
        if (!byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, null, out var b)) return false;
        return (0.299 * r + 0.587 * g + 0.114 * b) / 255 < 0.5;
    }

    // A seated party reads as "occupied" on the floor plan (yellow, same as a walk-in order) even
    // though the table's own record is still "Reserved" — the guest is physically there now.
    internal static TableStatus EffectiveTableStatus(TableDto t, ReservationDto? rv) =>
        rv?.Status == ReservationStatus.Seated ? TableStatus.Occupied : t.Status;

    // Icons mirror SHAPE_ICONS in floorplan.js so the button looks identical before and after a JS cycle.
    internal static string ShapeIcon(TableShape shape) => shape switch
    {
        TableShape.Round => "○",
        TableShape.Rectangular => "▭",
        TableShape.Oval => "⬭",
        TableShape.BarTable => "▬",
        _ => "◻"
    };

    // Which service turn a reservation's time falls into, so it reads at a glance in the
    // day list and on the floor plan without having to parse the raw HH:mm.
    internal static string PeriodIcon(ServicePeriod period) => period switch
    {
        ServicePeriod.Lunch => "sun",
        ServicePeriod.Dinner => "moon",
        _ => "clock"
    };

    // The group's display name: its own name if one was given when joining, else its members'
    // names in order ("T3 + T4").
    internal static string GroupDisplayName(IEnumerable<TableDto> tables, int gid)
    {
        var members = tables.Where(t => t.GroupId == gid).OrderBy(t => t.Name).ToList();
        var namedGroup = members.Select(t => t.GroupName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return namedGroup ?? string.Join(" + ", members.Select(t => t.Name));
    }

    // One small chair mark per seat, ringed around the table so it reads as a real table
    // instead of a bare labelled box. Round/oval tables get an even circular ring. Square/
    // rectangular tables follow the same convention as a real dining table: exactly one chair
    // on each short side (left/right) once there are at least 2 seats, and the rest split as
    // evenly as possible between the head and foot (top/bottom) — so 4 seats is 1-1-1-1, 6 is
    // 1-1-2-2, 8 is 1-1-3-3, etc., rather than a count proportional to each edge's length.
    // Percentages can fall outside 0–100 on purpose — that's what pushes the chair beyond the
    // table's own edge instead of sitting on top of it.
    // Joined tables are always snapped into a single left-to-right row (see JoinTablesAsync), so
    // a member's left and/or right edge can be a seam shared with its neighbour rather than a
    // real table edge — those sides skip their chair (hideLeft/hideRight, see
    // FloorGroupIndex.Seams) so nobody is drawn "sitting" inside the next table. Top/bottom are
    // never shared, since the row never stacks vertically.
    internal static IEnumerable<string> ChairStyles(TableDto t, bool hideLeft, bool hideRight)
    {
        const double outset = 18; // clearance off the table's own edge, scaled to the smaller chair size (see .tbl__chair)
        // Left/right offsets are a % of the table's own Width, so a wide 6+ seat table (see
        // RestaurantTable.DefaultWidthForSeats) pushed its side chairs much farther out in
        // absolute px than the top/bottom ones get from the same %. Pin the side offset to the
        // pixel clearance a default 70px-tall table would get instead, so it stays close
        // regardless of how wide the table is.
        var sideOutset = t.Width > 0 ? outset / 100 * 70 / t.Width * 100 : outset;
        var n = t.Seats;
        if (n <= 0 || n > 16) yield break;

        // Bar table: every stool along the top edge (the guests' side), none at the ends or the
        // back — rotate the table to face the stools another way. Pinned a fixed few px off the
        // edge rather than a % of the (short) height, so the stools sit tight against the bar.
        if (t.Shape == TableShape.BarTable)
        {
            for (var i = 0; i < n; i++)
                yield return $"left:{(i + 0.5) / n * 100:0.#}%; top:-14px; --ang:0deg;";
            yield break;
        }

        // A joined member always has its corners squared regardless of its own Shape (see
        // .tbl--grouped's border-radius:0 — the row has to read as one continuous rectangular
        // surface, not a chain of circles), so a round/oval seat ring — chairs at 30°/60° angles
        // meant to trace a circle's curve — would float at odd diagonal positions that don't line
        // up with that rectangle's straight edges or corners at all. Grouped members fall through
        // to the same rectangular one-per-side layout every square/rect table already uses.
        if (t.GroupId is null && t.Shape is TableShape.Round or TableShape.Oval)
        {
            for (var i = 0; i < n; i++)
            {
                var angle = 2 * Math.PI * i / n - Math.PI / 2;
                var left = 50 + 50 * (1 + outset / 50) * Math.Cos(angle);
                var top = 50 + 50 * (1 + outset / 50) * Math.Sin(angle);
                // Faces outward, away from the table centre — the same vector the chair was placed
                // along, just expressed as a rotation for the backrest pseudo-element (see .tbl__chair
                // in Tables.razor.css). +90 converts "angle from +X axis" to "rotation from facing up".
                var ang = angle * 180 / Math.PI + 90;
                yield return $"left:{left:0.#}%; top:{top:0.#}%; --ang:{ang:0.#}deg;";
            }
            yield break;
        }

        var sideCount = n >= 2 ? 1 : 0;
        var remaining = n - 2 * sideCount;
        var topCount = (remaining + 1) / 2;      // the odd remainder's extra chair goes to the head (top)
        var bottomCount = remaining / 2;

        if (!hideLeft)
            for (var i = 0; i < sideCount; i++)
                yield return $"left:{-sideOutset:0.#}%; top:50%; --ang:270deg;";          // left
        if (!hideRight)
            for (var i = 0; i < sideCount; i++)
                yield return $"left:{100 + sideOutset:0.#}%; top:50%; --ang:90deg;";      // right
        for (var i = 0; i < topCount; i++)
            yield return $"left:{(i + 0.5) / topCount * 100:0.#}%; top:{-outset:0.#}%; --ang:0deg;";
        for (var i = 0; i < bottomCount; i++)
            yield return $"left:{(i + 0.5) / bottomCount * 100:0.#}%; top:{100 + outset:0.#}%; --ang:180deg;";
    }
}

/// <summary>
/// Everything <see cref="FloorMap"/>'s output depends on, compared to decide whether it re-renders.
/// Lists are compared by reference, so the /tables page must replace (never mutate in place)
/// its table/zone/decor/reservation lists when they change. The selection set is mutated in
/// place, hence the content hash.
/// </summary>
public readonly record struct FloorRenderKey(
    object Tables, object Zones, object Decor, object Reservations, ServicePeriod? Period, bool Loaded,
    bool Editing, bool Selecting, int SelectionHash, int? AssignReservationId, string? ZoneFilter,
    bool ZoneMenuOpen, bool ZoneEditActive, int? SelectedAssignedTableId, int LayoutEpoch, object Settings)
{
    // Order-independent hash of the selected table ids (count included so {} and {0} differ).
    public static int HashSelection(IEnumerable<int> ids)
    {
        var hash = 0;
        var count = 0;
        foreach (var id in ids) { hash ^= id * 397; count++; }
        return HashCode.Combine(hash, count);
    }
}

/// <summary>
/// Per-render lookup of joined-table groups, built once from the full table list instead of every
/// group helper re-scanning all tables for each table/badge it draws.
/// </summary>
internal sealed class FloorGroupIndex
{
    // Members of each group ordered left-to-right — the snapped row order joins always produce.
    private readonly Dictionary<int, List<TableDto>> _byX;
    // First member in the original list order (not X order): any member carries the group's
    // reservation (see ResByTable), and this mirrors which one was used before.
    private readonly Dictionary<int, TableDto> _first;
    private readonly IReadOnlyList<TableDto> _tables;

    private FloorGroupIndex(IReadOnlyList<TableDto> tables)
    {
        _tables = tables;
        var grouped = tables.Where(t => t.GroupId is not null).ToList();
        _byX = grouped.GroupBy(t => t.GroupId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.PositionX).ToList());
        _first = grouped.GroupBy(t => t.GroupId!.Value).ToDictionary(g => g.Key, g => g.First());
    }

    public static FloorGroupIndex Build(IReadOnlyList<TableDto> tables) => new(tables);

    // Which of a joined member's shared edges (left/right) butt up against a neighbour.
    public (bool Left, bool Right) Seams(TableDto t)
    {
        if (t.GroupId is null || !_byX.TryGetValue(t.GroupId.Value, out var members)) return (false, false);
        var idx = members.FindIndex(m => m.Id == t.Id);
        return (idx > 0, idx >= 0 && idx < members.Count - 1);
    }

    // The data-seam attribute the CSS uses to drop the border/bevel on a shared edge, so the row
    // reads as one continuous table surface instead of a seam down the middle.
    public string SeamAttr(TableDto t)
    {
        var (left, right) = Seams(t);
        return left && right ? "both" : left ? "left" : right ? "right" : "";
    }

    // The anchor is the left-most (then top-most) member — it carries the group's edit button.
    public bool IsAnchor(TableDto t)
    {
        if (t.GroupId is null || !_byX.TryGetValue(t.GroupId.Value, out var members)) return false;
        var minX = members.Min(m => m.PositionX);
        return t.PositionX == minX && t.PositionY == members.Where(m => m.PositionX == minX).Min(m => m.PositionY);
    }

    public ReservationDto? Reservation(int gid, Dictionary<int, ReservationDto> resByTable) =>
        _first.TryGetValue(gid, out var member) && resByTable.TryGetValue(member.Id, out var rv) ? rv : null;

    // Joining tables loses a seat on each side of every seam where two tables meet (nobody can
    // sit facing into the next table) — a chain of N tables has N-1 seams, so 2 seats go for
    // each one. Two 4-seat tables joined into one therefore seat 6, not 8.
    public int Seats(int gid)
    {
        if (!_byX.TryGetValue(gid, out var members)) return 0;
        if (members.Count <= 1) return members.Sum(t => t.Seats);
        return Math.Max(0, members.Sum(t => t.Seats) - 2 * (members.Count - 1));
    }

    public string DisplayName(int gid) => FloorPlanHelpers.GroupDisplayName(_tables, gid);
}
