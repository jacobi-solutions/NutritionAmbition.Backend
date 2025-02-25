using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
namespace NutritionAmbition.Backend.API.Middleware
{
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors)
                    .Select(x => x.ErrorMessage)    
                    .ToList();

                var response = new Response();
                errors.ForEach(error => response.AddError(error, ErrorTypes.VALIDATION_ERROR));

                context.Result = new BadRequestObjectResult(response);
            }
        }
    }
}
