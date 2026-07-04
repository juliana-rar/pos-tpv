using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record TableDto(
    int Id, string Name, int Seats, TableShape Shape, TableStatus Status,
    double PositionX, double PositionY, double Width, double Height, double Rotation, bool IsLocked,
    int? ActiveOrderId, decimal ActiveOrderTotal, int? GroupId);

public class TableFormDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Seats { get; set; } = 4;
    public TableShape Shape { get; set; } = TableShape.Square;
}

/// <summary>Geometry (and shape) patch pushed when the floor map is saved.</summary>
public record TableLayoutDto(int Id, double PositionX, double PositionY, double Width, double Height, double Rotation, bool IsLocked, TableShape Shape = TableShape.Square);
