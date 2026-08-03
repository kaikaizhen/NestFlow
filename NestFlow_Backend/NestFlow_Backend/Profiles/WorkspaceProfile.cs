using AutoMapper;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Profiles;

/// <summary>
/// Workspace 與使用者模組的模型轉換。列舉一律轉為小寫字串輸出給前端。
/// </summary>
public class WorkspaceProfile : Profile
{
    public WorkspaceProfile()
    {
        CreateMap<WorkspaceDtoModel, WorkspaceViewModel>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString().ToLowerInvariant()))
            .ForMember(d => d.MembershipType, o => o.MapFrom(s => s.MembershipType.ToString().ToLowerInvariant()));

        CreateMap<WorkspaceMemberDtoModel, WorkspaceMemberViewModel>()
            .ForMember(d => d.MembershipType, o => o.MapFrom(s => s.MembershipType.ToString().ToLowerInvariant()));

        CreateMap<InvitationDtoModel, InvitationViewModel>();

        CreateMap<CurrentUserDtoModel, CurrentUserViewModel>();
    }
}
