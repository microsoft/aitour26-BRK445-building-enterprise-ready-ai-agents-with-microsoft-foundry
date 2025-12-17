using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using ZavaDatabaseInitialization;

namespace Infra.AgentDeployment;

public static class DbInitializationHelper
{
    /// <summary>
    /// Initializes the database using the connection string from TaskTracker
    /// </summary>
    /// <param name="taskTracker">The task tracker for UI updates</param>
    /// <returns>True if initialization was successful, false otherwise</returns>
    public static async Task<bool> InitializeDatabaseAsync(TaskTracker taskTracker)
    {
        try
        {
            taskTracker.StartTask("Database Initialization");
            
            // Get connection string from TaskTracker
            string connectionString = taskTracker.GetSqlServerConnectionString();
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                taskTracker.AddLog("[yellow]Database initialization skipped - no connection string provided[/]");
                taskTracker.CompleteTask("Database Initialization");
                return false;
            }

            taskTracker.AddLog("[grey]Testing database connection...[/]");
            
            // Create DbContext with the provided connection string
            var optionsBuilder = new DbContextOptionsBuilder<Context>();
            optionsBuilder.UseSqlServer(connectionString);
            
            using (var context = new Context(optionsBuilder.Options))
            {
                // Test connection
                var (success, errorMessage) = await TestConnectionAsync(context, taskTracker);
                if (!success)
                {
                    taskTracker.AddLog("[red]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
                    taskTracker.AddLog("[red bold]DATABASE CONNECTION FAILED[/]");
                    taskTracker.AddLog("[red]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
                    
                    // Always show error details, even if empty
                    if (string.IsNullOrWhiteSpace(errorMessage))
                    {
                        taskTracker.AddLog("[red]  No error details available[/]");
                    }
                    else
                    {
                        // Split long error messages into multiple lines for better readability
                        var errorLines = SplitErrorMessage(errorMessage);
                        if (errorLines.Count == 0)
                        {
                            taskTracker.AddLog($"[red]  {errorMessage}[/]");
                        }
                        else
                        {
                            foreach (var line in errorLines)
                            {
                                taskTracker.AddLog($"[red]{line}[/]");
                            }
                        }
                    }
                    
                    taskTracker.AddLog("[red]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
                    taskTracker.AddLog("[yellow]Continuing without database initialization...[/]");
                    taskTracker.CompleteTask("Database Initialization");
                    
                    // Give user time to read the error
                    await Task.Delay(3000);
                    return false;
                }
                
                taskTracker.AddLog("[green]✓[/] Database connection successful");
                
                // Create database and tables
                taskTracker.AddLog("[grey]Creating database and tables...[/]");
                await context.Database.EnsureCreatedAsync();
                taskTracker.AddLog("[green]✓[/] Database and tables created");
                
                // Initialize data
                taskTracker.AddLog("[grey]Initializing data...[/]");
                DbInitializer.Initialize(context);
                taskTracker.AddLog("[green]✓[/] Data initialized successfully");
            }
            
            taskTracker.CompleteTask("Database Initialization");
            taskTracker.AddLog("[green]✓ Database initialization completed![/]");
            return true;
        }
        catch (Exception ex)
        {
            taskTracker.AddLog($"[red]Error during database initialization: {ex.Message}[/]");
            taskTracker.CompleteTask("Database Initialization");
            return false;
        }
    }

    /// <summary>
    /// Tests the database connection
    /// </summary>
    /// <returns>Tuple with success status and error message if failed</returns>
    private static async Task<(bool success, string errorMessage)> TestConnectionAsync(Context context, TaskTracker taskTracker)
    {
        try
        {
            await context.Database.OpenConnectionAsync();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            // Build detailed error message
            var errorMessage = $"Error: {ex.Message}";
            
            // Include inner exception details if available
            if (ex.InnerException != null)
            {
                errorMessage += $" | Inner: {ex.InnerException.Message}";
            }
            
            taskTracker.AddLog($"[red]Connection test failed: {ex.GetType().Name}[/]");
            return (false, errorMessage);
        }
    }

    /// <summary>
    /// Splits a long error message into multiple lines for better readability
    /// </summary>
    private static List<string> SplitErrorMessage(string message)
    {
        var lines = new List<string>();
        
        // Handle null or empty
        if (string.IsNullOrWhiteSpace(message))
        {
            return lines;
        }
        
        const int maxLineLength = 85; // Slightly reduced for better fit
        
        if (message.Length <= maxLineLength)
        {
            lines.Add($"  {message}");
            return lines;
        }
        
        // Split by common delimiters first
        var parts = message.Split(new[] { " | Inner: " }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            lines.Add($"  {message}");
            return lines;
        }
        
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            
            var prefix = part == parts[0] ? "  " : "  ➜ "; // Use arrow for inner exception
            
            if (part.Length + prefix.Length <= maxLineLength)
            {
                lines.Add($"{prefix}{part.Trim()}");
            }
            else
            {
                // Split long parts into chunks
                var words = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    lines.Add($"{prefix}{part.Trim()}");
                    continue;
                }
                
                var currentLine = prefix;
                
                foreach (var word in words)
                {
                    if (currentLine.Length + word.Length + 1 <= maxLineLength)
                    {
                        currentLine += (currentLine == prefix ? "" : " ") + word;
                    }
                    else
                    {
                        if (currentLine.Length > prefix.Length)
                        {
                            lines.Add(currentLine);
                        }
                        currentLine = "    " + word; // Indent continuation lines
                    }
                }
                
                if (currentLine.Length > 2)
                {
                    lines.Add(currentLine);
                }
            }
        }
        
        // Fallback if no lines were added
        if (lines.Count == 0)
        {
            lines.Add($"  {message}");
        }
        
        return lines;
    }
}
