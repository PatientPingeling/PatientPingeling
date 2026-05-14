using FluentValidation;
using NotificationService.Api.Contracts;

namespace NotificationService.Api.Validators
{
  public sealed class AppointmentWebhookRequestValidator : AbstractValidator<AppointmentWebhookRequest>
  {
    public AppointmentWebhookRequestValidator()
    {
      // 1. Top-level safety
      // TODO: Possibly remove (fail if unknown) when service can figure it out himself
      RuleFor(x => x.Action).NotEqual(AppointmentAction.UNKNOWN);

      // 2. Use nested validators for better organization
      RuleFor(x => x.Patient)
          .NotNull()
          .SetValidator(new PatientValidator());

      RuleFor(x => x.Appointment)
          .NotNull()
          .SetValidator(new AppointmentDetailsValidator());
    }
  }

  public sealed class PatientValidator : AbstractValidator<AppointmentPatientDto>
  {
    public PatientValidator()
    {
      RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(100);
      RuleFor(x => x.GivenName).NotEmpty().MaximumLength(200);

      // Add format validation for optional fields
      RuleFor(x => x.Email)
          .EmailAddress()
          .When(x => !string.IsNullOrEmpty(x.Email));
    }
  }

  public sealed class AppointmentDetailsValidator : AbstractValidator<AppointmentDetailsDto>
  {
    public AppointmentDetailsValidator()
    {
      RuleFor(x => x.ExternalId).NotEmpty();
      RuleFor(x => x.Location).NotEmpty().MaximumLength(500);

      // Strict date check
      RuleFor(x => x.ScheduledAt)
          .GreaterThan(DateTimeOffset.UnixEpoch)
          .WithMessage("Date is too far in the past.");
    }
  }
}

