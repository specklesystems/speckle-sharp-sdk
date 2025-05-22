# Coding standards, domain knowledge, and preferences that AI should follow

## C# Coding Standards

- Use the csharpier formatter for formatting C# code.
- Use the .editorconfig file for code style settings.
- Always use `var` when the type is obvious from the right side of the assignment.
- Always add braces for `if`, `else`, `for`, `foreach`, `while`, and `do` statements, even if they are single-line statements.

## Testing

- Use xUnit for unit testing.
- Use FluentAssertions for assertions in tests.
- Use Moq for mocking dependencies in tests.
