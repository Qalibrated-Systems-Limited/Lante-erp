using AutoMapper;
using HrService.Core.DTOs.Employees;
using HrService.Core.Entities;

namespace HrService.Core.Mappings;

/// <summary>AutoMapper profile for the HR &amp; Payroll module (Module 3).
/// Entity↔DTO maps are added per phase. Enum→string maps by convention.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // H1 — employee master. Collections, computed flags and cross-service names are filled in by the
        // service, which has the context to resolve them.
        CreateMap<Employee, EmployeeReadDto>()
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.EmploymentType, o => o.Ignore())
            .ForMember(d => d.FullName, o => o.Ignore())
            .ForMember(d => d.ReportsToName, o => o.Ignore())
            .ForMember(d => d.HasUserAccount, o => o.Ignore())
            .ForMember(d => d.MissingMandatoryDocuments, o => o.Ignore())
            .ForMember(d => d.HasExpiredCertification, o => o.Ignore())
            .ForMember(d => d.EmergencyContacts, o => o.Ignore())
            .ForMember(d => d.Education, o => o.Ignore())
            .ForMember(d => d.EmploymentHistory, o => o.Ignore())
            .ForMember(d => d.Documents, o => o.Ignore())
            .ForMember(d => d.BankDetails, o => o.Ignore())
            .ForMember(d => d.Certifications, o => o.Ignore());
    }
}
