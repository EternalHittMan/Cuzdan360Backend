using System;

namespace Cuzdan360Backend.Exceptions;

public class CustomException : Exception
{
    public int StatusCode { get; }

    public CustomException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}