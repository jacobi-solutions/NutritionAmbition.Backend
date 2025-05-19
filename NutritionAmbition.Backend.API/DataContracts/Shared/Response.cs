using System;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class Response
    {
        public List<Error> Errors { get; set; } = new List<Error>();
        public bool IsSuccess { get; set; } = true;
        public string? CorrelationId { get; set; } = Guid.NewGuid().ToString();
        public string? StackTrace { get; set; }
        public string? AccountId { get; set; }

        public void AddError(string message, string? errorCode = null)
        {
            IsSuccess = false;
            Errors.Add(new Error { ErrorMessage = message, ErrorCode = errorCode });
        }

        public void SetErrorMessageIfError(bool isError, string errorMessage, string? errorCode = null)
        {
            if (isError)
            {
                AddError(errorMessage, errorCode);
            }
        }

        public void CaptureException(Exception ex, bool includeStackTrace = false)
        {
            AddError(ex.Message, "EXCEPTION");
            if (includeStackTrace)
            {
                StackTrace = ex.StackTrace;
            }
        }
    }
}
