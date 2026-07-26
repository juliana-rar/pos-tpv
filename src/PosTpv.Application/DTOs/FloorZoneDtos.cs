namespace PosTpv.Application.DTOs;

public record FloorZoneDto(int Id, string Name, double PositionX, double PositionY, double Width, double Height, string? Color);

/// <summary>Geometry patch pushed when the floor map is saved (mirrors TableLayoutDto).</summary>
public record FloorZoneLayoutDto(int Id, double PositionX, double PositionY, double Width, double Height);
