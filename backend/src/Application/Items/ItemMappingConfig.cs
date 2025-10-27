using System.Linq;
using Contracts.DTO.Items;
using Domain.Entities.Items;
using Mapster;

namespace Application.Items;

public class ItemMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Item, ItemResponse>()
            .Map(dest => dest.CategoryName, src => src.Category != null ? src.Category.Name : string.Empty)
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.Images, src => src.Images.OrderBy(image => image.SortOrder));

        config.NewConfig<ListingImage, ListingImageResponse>();
    }
}
