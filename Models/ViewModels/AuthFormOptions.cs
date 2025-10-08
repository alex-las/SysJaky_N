using SysJaky_N.Pages.Account;

namespace SysJaky_N.Models.ViewModels;

public abstract class AuthFormOptionsBase<TInput>
    where TInput : class, new()
{
    public TInput Input { get; set; } = new();
    public string Method { get; set; } = "post";
    public string? FormId { get; set; }
    public string? FormCssClass { get; set; }
    public string? AspPage { get; set; }
    public string? AspPageHandler { get; set; }
    public string? Action { get; set; }
    public bool IncludeAntiforgery { get; set; } = true;
    public string? HeadingLocalizationKey { get; set; }
    public string HeadingTagName { get; set; } = "h1";
    public string? HeadingId { get; set; }
    public string? ValidationSummaryCssClass { get; set; } = "text-danger";
    public string AltchaChallengeUrl { get; set; } = "~/altcha/challenge?d=2";
    public string AltchaVerifyUrl { get; set; } = "~/altcha/verify";
    public string AltchaWorkerUrl { get; set; } = "~/lib/altcha/altcha.worker.js";
    public bool UseAbsoluteWorkerUrl { get; set; } = true;
    public string? AltchaWorkerVersionSuffix { get; set; } = "?v=5";
    public string AltchaWorkers { get; set; } = "1";
    public string AltchaMaxNumber { get; set; } = "500000";
    public bool EnableRefetchOnExpire { get; set; } = true;
    public string AltchaWidgetName { get; set; } = "altcha";
    public string? AltchaId { get; set; }
}

public class LoginFormOptions : AuthFormOptionsBase<LoginModel.InputModel>
{
    public string EmailLabelKey { get; set; } = "EmailLabel";
    public string PasswordLabelKey { get; set; } = "PasswordLabel";
    public string RememberMeLabelKey { get; set; } = "RememberMe";
    public string SubmitButtonKey { get; set; } = "LoginTitle";
    public string EnableJavascriptKey { get; set; } = "EnableJavascript";
    public string? EmailInputId { get; set; }
    public string? PasswordInputId { get; set; }
    public string? RememberMeInputId { get; set; }
    public bool ShowRememberMe { get; set; } = true;
}

public class RegisterFormOptions : AuthFormOptionsBase<RegisterModel.InputModel>
{
    public string EmailLabelKey { get; set; } = "EmailLabel";
    public string PasswordLabelKey { get; set; } = "PasswordLabel";
    public string ConfirmPasswordLabelKey { get; set; } = "ConfirmPasswordLabel";
    public string ReferralCodeLabelKey { get; set; } = "ReferralCodeLabel";
    public string SubmitButtonKey { get; set; } = "RegisterTitle";
    public string EnableJavascriptKey { get; set; } = "EnableJavascript";
    public string? EmailInputId { get; set; }
    public string? PasswordInputId { get; set; }
    public string? ConfirmPasswordInputId { get; set; }
    public string? ReferralCodeInputId { get; set; }
    public bool IncludeConfirmPassword { get; set; }
    public bool IncludeReferralCode { get; set; }
}
