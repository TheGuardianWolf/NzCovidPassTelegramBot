namespace NzCovidPassTelegramBot.Data.Templates
{
    public class BotText
    {
        public const string HelpInfo = @"This bot links a NZ Covid pass with your Telegram account.

The goal is to provide a way to share vaccine status without revealing your identity. This may be useful for event organisation.

Please be aware of the limitations of the Covid pass, it is unable to uniquely identify an individual. Photo ID checks should be required to confirm vaccine status for events where they are needed by law.

This service will store your Telegram ID and a hash of your Covid pass ID for the link.

Please visit {0} for more details.

Usage:
";

        public const string LinkInfo = @"{0}

If you would like to link or update a NZ Covid pass, please upload an image containing the QR code from your Covid pass.

You may use a screenshot or photo of the QR code.";

        public const string CovidPassCheck = @"*NZ Covid Pass Check - {0}*

@{1} has requested Covid pass statuses for members of this chat.

Message @{2} if you haven't already to link your Covid pass.

With a valid pass, clicking the button below will reveal that you have a valid Covid pass, no other information is given.

Members valid: {3}.";

        public const string VerifyPassInfo = @"This Covid pass can be linked to your account, please check its details are correct. These details will not be saved.

Given name: {0}
Family name: {1}
Date of birth: {2}
Valid from: {3}
Valid to: {4}

Unique link code: `{5}`
";

        public const string NotariseConfirm = @"Please confirm that you would like to notarise this user.

UserId: {0}
Username: {1}
Pass valid from: {2}
Pass valid to: {3}";

        public const string RevokeNotariseConfirm = @"Please confirm that you would like to revoke your notarisation for this user.

UserId: {0}
Username: {1}
Pass valid from: {2}
Pass valid to: {3}";

        public const string NotLinkedPreamble = "Your Telegram account is *NOT LINKED* to a Covid pass.";
        public const string LinkedPreamble = "Your Telegram account is *LINKED* to a Covid pass";

        public const string NotariseInfo = @"Due to your status as a notary, you may choose to mark linked accounts in the system as being _notarised_.

This will show up on checks and provide an extra level of verification.

You should only mark individuals that have presented photo id and their vaccine passport. Abuse of this system will cause your status to be removed.

Please forward a message from the account to notarise. You may also remove their notarised status through this method.";

        public const string CheckInfo = @"Click the button below to request a group or user's vaccine pass link status\(es\).

This feature can also be used by mentioning `@{0}` in any private or group chats to initiate a poll.

This will prompt users to check in with their Covid pass status.";
    }
}
