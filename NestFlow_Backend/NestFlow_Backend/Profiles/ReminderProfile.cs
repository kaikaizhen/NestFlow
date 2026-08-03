using AutoMapper;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Profiles;

/// <summary>提醒模組的模型轉換。列舉一律轉為小寫字串輸出給前端。</summary>
public class ReminderProfile : Profile
{
    public ReminderProfile()
    {
        CreateMap<ReminderDtoModel, ReminderViewModel>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString().ToLowerInvariant()));
    }
}
