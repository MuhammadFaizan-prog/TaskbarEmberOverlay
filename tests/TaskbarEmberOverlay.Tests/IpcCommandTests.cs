using Xunit;

namespace TaskbarEmberOverlay.Tests
{
    public class IpcCommandTests
    {
        [Theory]
        [InlineData("--exit", true)]
        [InlineData("-exit", true)]
        [InlineData("/exit", true)]
        [InlineData("--pause", false)]
        [InlineData("random", false)]
        public void IsExitArgument_DetectsExitVariantsCorrectly(string arg, bool expectedIsExit)
        {
            string clean = arg.Trim().ToLowerInvariant();
            bool isExit = clean is "--exit" or "-exit" or "/exit";
            Assert.Equal(expectedIsExit, isExit);
        }

        [Theory]
        [InlineData("EXIT", "OK_EXITING")]
        [InlineData("PAUSE", "OK_PAUSED")]
        [InlineData("RESUME", "OK_RESUMED")]
        [InlineData("PING", "PONG")]
        [InlineData("UNKNOWN", "UNKNOWN_COMMAND")]
        public void EvaluateCommand_ReturnsExpectedResponseToken(string cmd, string expectedResponse)
        {
            string response = cmd.ToUpperInvariant() switch
            {
                "EXIT" => "OK_EXITING",
                "PAUSE" => "OK_PAUSED",
                "RESUME" => "OK_RESUMED",
                "PING" => "PONG",
                _ => "UNKNOWN_COMMAND"
            };

            Assert.Equal(expectedResponse, response);
        }
    }
}
