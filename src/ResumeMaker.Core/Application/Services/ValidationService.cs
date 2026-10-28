using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;

namespace ResumeMaker.Core.Application.Services;

public sealed class ValidationService
{
    public ValidationResult Validate(Profile profile)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            issues.Add(new("name_required", "Profile name is required.", true));
        }
        if (string.IsNullOrWhiteSpace(profile.Contact.FullName))
        {
            issues.Add(new("full_name_required", "Full name is required.", true));
        }
        if (!string.IsNullOrWhiteSpace(profile.Contact.Email) && !IsValidEmail(profile.Contact.Email))
        {
            issues.Add(new("email_invalid", $"Email '{profile.Contact.Email}' is not valid.", true));
        }
        ValidateLinks(profile, issues);
        if (profile.Language is not ("fa" or "en"))
        {
            issues.Add(new("language_invalid", $"Language '{profile.Language}' must be 'fa' or 'en'.", true));
        }
        if (string.IsNullOrWhiteSpace(profile.TemplateId))
        {
            issues.Add(new("template_required", "A template must be selected.", true));
        }
        if (!profile.Sections.Any(s => s.Enabled))
        {
            issues.Add(new("no_enabled_sections", "No section is enabled; the resume will be empty.", false));
        }
        return new ValidationResult(issues);
    }

    public ValidationResult ValidateCoverLetter(CoverLetter letter)
    {
        var result = CoverLetterService.Validate(letter);
        var issues = result.Issues.ToList();
        if (string.IsNullOrWhiteSpace(letter.Company))
        {
            issues.Add(new("company_required", "Company is required.", false));
        }
        if (string.IsNullOrWhiteSpace(letter.Position))
        {
            issues.Add(new("position_required", "Position is required.", false));
        }
        return new ValidationResult(issues);
    }

    private static void ValidateLinks(Profile profile, List<ValidationIssue> issues)
    {
        foreach (var (key, value) in profile.Contact.Links)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }
            var ok =
                Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!ok)
            {
                issues.Add(new("link_invalid", $"Link '{key}' must start with http:// or https://.", true));
            }
        }
    }

    public static bool IsValidEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || email.Any(char.IsWhiteSpace))
        {
            return false;
        }
        var domain = email[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 1 && !domain.StartsWith('.');
    }
}
