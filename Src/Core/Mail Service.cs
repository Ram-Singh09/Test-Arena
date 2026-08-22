using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

sealed class Gmail_Details
{
    public readonly string Id, Password;

    public Gmail_Details(string Id, string Password)
    {
        this.Id = Id;
        this.Password = Password;
    }
}

static class Mail_Service
{
    private static Gmail_Details Mail_info; // server mail
    private const string Mail_Server_Name = "Test Arena",
    Smtp_Addr = "smtp.gmail.com"; // where we send email then it sends further

    public static void Set_Mail_Info(WebApplicationBuilder Builder, string Mail_name)
    {
        if (Builder is null) throw new NullReferenceException("Mail Service - Builder provided is null");

        // get mail details
        var Id = Builder.Configuration["Gmail:Id"];
        var Password = Builder.Configuration["Gmail:Password"];

        if (Id is null || Password is null) throw new NullReferenceException("Mail Service - Config does not has mail id or password or wrong name used");

        // get info and done
        Mail_info = new (Id, Password);
    }

    public static int Gen_OTP() => Random.Shared.Next(111111, 999999);
    

    /// <param name="To">Email id of reciver</param>
    /// <returns>True if completes successfully</returns>
    public static async ValueTask<bool> Send_OTP(string To, int Val, string Client_name)
    {
        try
        {
            var Msg = new MimeMessage();
            Msg.From.Add(new MailboxAddress(Mail_Server_Name, Mail_info.Id));
            Msg.To.Add(new MailboxAddress(Client_name, To));
            Msg.Subject = "Test Arena OTP for verification";
            Msg.Body = new TextPart("plain") { Text = $"OTP = {Val}" };

            // connect to Smtp server to send data
            using var Client = new MailKit.Net.Smtp.SmtpClient();
            await Client.ConnectAsync(Smtp_Addr, 587, SecureSocketOptions.StartTls);

            // set mail id and password(made with App Password, it can be deleted)
            await Client.AuthenticateAsync(Mail_info.Id, Mail_info.Password);

            // send packet to middle man google relay which then sends to target email
            await Client.SendAsync(Msg);
            await Client.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SMTP ERROR]: {ex.Message}");
            Console.WriteLine(ex.StackTrace);

            return false;
        }

    }
}
