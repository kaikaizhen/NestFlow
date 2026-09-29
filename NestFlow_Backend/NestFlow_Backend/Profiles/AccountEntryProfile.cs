using AutoMapper;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Profiles;

/// <summary>記帳模組的模型轉換。列舉一律轉為小寫字串輸出給前端。</summary>
public class AccountEntryProfile : Profile
{
    public AccountEntryProfile()
    {
        CreateMap<AccountEntryDtoModel, AccountEntryViewModel>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString().ToLowerInvariant()));

        CreateMap<CurrencySummaryDtoModel, CurrencySummaryViewModel>();
        CreateMap<AccountEntryShareDtoModel, AccountEntryShareViewModel>();
    }
}
