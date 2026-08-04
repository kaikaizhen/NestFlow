using AutoMapper;
using NestFlow_Backend.Models.Dtos;
using NestFlow_Backend.Models.ViewModels;

namespace NestFlow_Backend.Profiles;

/// <summary>儲藏庫模組的模型轉換。</summary>
public class StorageItemProfile : Profile
{
    public StorageItemProfile()
    {
        CreateMap<StorageItemDtoModel, StorageItemViewModel>();
    }
}
