using Microsoft.AspNetCore.Mvc.Filters;
using AccountingLab.Domain.Model;

namespace AccountingLab.Api.Transport;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ChallengeInputFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ArgumentException or FormatException)
            context.Exception = new BusinessRuleException(context.Exception.Message);
    }
}
