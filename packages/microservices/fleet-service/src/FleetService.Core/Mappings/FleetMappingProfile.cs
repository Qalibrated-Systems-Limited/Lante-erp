using System.Text.Json;
using AutoMapper;
using FleetService.Core.DTOs.Trip;
using FleetService.Core.Entities;

namespace FleetService.Core.Mappings;

public class FleetMappingProfile : Profile
{
    public FleetMappingProfile()
    {
        CreateMap<Trip, TripResponseDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.TripTypeName, o => o.MapFrom(s => s.TripType != null ? s.TripType.Name : null))
            .ForMember(d => d.MaterialName, o => o.MapFrom(s => s.Material != null ? s.Material.Name : null))
            .ForMember(d => d.TripPhotos, o => o.MapFrom(s => DeserializePhotos(s.TripPhotosJson)));

        CreateMap<CreateTripDto, Trip>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.TotalCost, o => o.Ignore())
            .ForMember(d => d.Profit, o => o.Ignore());

        CreateMap<UpdateTripDto, Trip>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.TruckId, o => o.Ignore())
            .ForMember(d => d.DriverId, o => o.Ignore())
            .ForMember(d => d.TripTypeId, o => o.Ignore());
    }

    private static List<string> DeserializePhotos(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
