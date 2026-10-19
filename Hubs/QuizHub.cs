using Microsoft.AspNetCore.SignalR;

public class QuizHub : Hub
{
    public async Task SendQuestion(Question question)
    {
        await Clients.All.SendAsync("ReceiveQuestion", question);
    }

    public async Task SubmitAnswer(string user, int questionId, int selectedOption)
    {
        // Logika penilaian dan update skor bisa ditambahkan di sini
        await Clients.All.SendAsync("UpdateScoreboard", user, /*score*/ 0);
    }
}