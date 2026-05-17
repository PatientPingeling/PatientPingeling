namespace NotificationService.Domain
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        protected Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
                throw new InvalidOperationException("Success result cannot have an error.");
            if (isSuccess is false && error == Error.None)
                throw new InvalidOperationException("Failure result must have an error.");

            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);
    }

    public class Result<TValue> : Result
    {
        private readonly TValue _value;

        protected internal Result(TValue value, bool isSuccess, Error error) : base(isSuccess, error)
        {
            _value = value;
        }

        public TValue Value => IsSuccess ? _value : throw new InvalidOperationException("The value of a failure result can not be accessed.");

        public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
        public static new Result<TValue> Failure(Error error) => new(default!, false, error);
    }

    public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure) // TODO: Might add support for Exception consumption
    {
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    }

    public enum ErrorType
    {
        Validation,   // 400
        NotFound,     // 404
        Conflict,     // 409
        Unauthorized, // 401
        Forbidden,    // 403
        Failure,      // 500
        Duplicate     // 200 — already exists, idempotent skip
    }
}