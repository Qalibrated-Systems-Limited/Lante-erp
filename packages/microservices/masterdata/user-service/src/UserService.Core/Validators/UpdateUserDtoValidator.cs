using FluentValidation;
using UserService.Core.DTOs.Users;

namespace UserService.Core.Validators;

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        When(x => x.FirstName != null, () => RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100));
        When(x => x.LastName != null, () => RuleFor(x => x.LastName).NotEmpty().MaximumLength(100));
        When(x => x.MobileNumber != null, () => RuleFor(x => x.MobileNumber).MaximumLength(20));
    }
}
