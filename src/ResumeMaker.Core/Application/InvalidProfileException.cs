namespace ResumeMaker.Core.Application;

public class InvalidProfileException(string message, Exception? inner = null) : Exception(message, inner);
