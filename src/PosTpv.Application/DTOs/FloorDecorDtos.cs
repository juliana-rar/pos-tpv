using PosTpv.Domain.Enums;

namespace PosTpv.Application.DTOs;

public record FloorDecorDto(
    int Id, FloorDecorType Type,
    double PositionX, double PositionY, double Width, double Height, double Rotation, bool IsLocked);

/// <summary>Geometry patch pushed when the floor map is saved (mirrors TableLayoutDto).</summary>
public record FloorDecorLayoutDto(int Id, double PositionX, double PositionY, double Width, double Height, double Rotation, bool IsLocked);
