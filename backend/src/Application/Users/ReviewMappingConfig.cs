using Contracts.DTO.Users;
using Domain.Entities.Users;
using Mapster;

namespace Application.Users;

public class ReviewMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Review, ReviewResponse>()
            .Map(dest => dest.ReviewerName, src => src.Reviewer != null ? src.Reviewer.DisplayName : "Unknown")
            .Map(dest => dest.TargetUserName, src => src.TargetUser != null ? src.TargetUser.DisplayName : "Unknown");
    }
}
