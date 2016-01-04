using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emmily.DataProviders
{
    public interface IUserResponseConnection
    {
        Task<bool> RespondAsync(string text, bool silent = false);
        Task<bool> RespondAsync(string text, string subtext, bool silent = false);
        Task<bool> RespondAsync(string text, Uri imageUri, bool silent = false);

        Task<string> AskForResponseAsync(string text);
        Task<string> AskForResponseAsync(string text, string subtext);
        Task<string> AskForResponseAsync(string text, Uri imageUri);
    }
}
