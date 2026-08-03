using AutoMapper;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Profiles;

/// <summary>行程模組的模型轉換。</summary>
public class CalendarEventProfile : Profile
{
    public CalendarEventProfile()
    {
        CreateMap<CalendarEventDtoModel, CalendarEventViewModel>();
    }
}
