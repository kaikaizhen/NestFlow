using AutoMapper;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Profiles;

/// <summary>代辦模組的模型轉換。列舉一律轉為小寫字串輸出給前端。</summary>
public class TodoProfile : Profile
{
    public TodoProfile()
    {
        CreateMap<TodoDtoModel, TodoViewModel>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString().ToLowerInvariant()))
            .ForMember(d => d.IsCompleted, o => o.MapFrom(s => s.CompletedAt != null));
    }
}
