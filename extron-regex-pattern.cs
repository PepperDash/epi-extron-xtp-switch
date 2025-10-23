// Regex pattern for parsing Extron switcher route command responses
// Format: "Out[Index] In[Index] [All/Video/Audio]\n"

// C# Regex Pattern:
@"^Out(\d+) In(\d+) (All|Video|Audio)\r?\n?$"

// Pattern Breakdown:
// ^Out         - Start of string + literal "Out"
// (\d+)        - Capture group 1: Output index (one or more digits)
// In           - Literal " In" (space + "In")
// (\d+)        - Capture group 2: Input index (one or more digits)
//              - Literal space
// (All|Video|Audio) - Capture group 3: Route type (exactly one of these options)
// \r?\n?       - Optional carriage return + optional newline
// $            - End of string

// Capture Groups:
// Group 1: Output index (number)
// Group 2: Input index (number)
// Group 3: Route type (All/Video/Audio)

// Example matches:
// "Out1 In5 All\n"      → Groups: ("1", "5", "All")
// "Out12 In3 Video\r\n" → Groups: ("12", "3", "Video")
// "Out8 In15 Audio"     → Groups: ("8", "15", "Audio")

// Usage example in C#:
/*
using System.Text.RegularExpressions;

string pattern = @"^Out(\d+) In(\d+) (All|Video|Audio)\r?\n?$";
Regex regex = new Regex(pattern);

string response = "Out1 In5 All\n";
Match match = regex.Match(response);

if (match.Success)
{
    int outputIndex = int.Parse(match.Groups[1].Value);
    int inputIndex = int.Parse(match.Groups[2].Value);
    string routeType = match.Groups[3].Value;
    
    // Process the parsed values...
}
*/
