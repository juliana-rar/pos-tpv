namespace PosTpv.Application.DTOs;

public record TopProductDto(string Name, int Quantity, decimal Revenue);

public record DashboardDto(
    decimal SalesToday, decimal SalesMonth, int OrdersToday, int OpenOrders,
    int OccupiedTables, int TotalTables, int ReservationsToday,
    IReadOnlyList<TopProductDto> TopProducts);
