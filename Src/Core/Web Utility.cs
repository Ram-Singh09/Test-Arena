using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

sealed class Active_OTP
{
    public DateTime Finish_time;
    public int OTP;
    public byte Attempt_count;
    public OTP_Type Otp_type;

    public Active_OTP(byte Minutes, int Otp, OTP_Type Ot_type)
    {
        Finish_time = DateTime.UtcNow.AddMinutes(Minutes);
        OTP = Otp;
        Attempt_count = 0;
    }
}

static class Web_Utility
{
    // salt is extra randomizer, itters make it complex and hash size is how long it is
    private const int Hash_Salt_Size = 16, Hash_Size = 32, Hash_Iterations = 100_000;
    private const int Nonce_Size = 12, Tag_Size = 16;
    private static readonly JwtSecurityTokenHandler JWT_token_handler = new();
    
    public static string Hash_Email(string Email, byte[] Hashed_key)
    {
        // lower case for same every time ever where
        var Clean_email = Email.Trim().ToLowerInvariant();
        var Email_bytes = Encoding.UTF8.GetBytes(Clean_email);

        // hasher but not as heavy as other one
        using var Hmac = new HMACSHA256(Hashed_key);
        var Hashed_email = Hmac.ComputeHash(Email_bytes);

        // deterministic string
        return Convert.ToHexString(Hashed_email);
    }

    // result str 1st part is salt and 2nd hash
    public static string Hash_Password(string Value)
    {
        // random salt
        byte[] Salt = RandomNumberGenerator.GetBytes(Hash_Salt_Size);

        // Compute PBKDF2 hash using this salt
        byte[] Hash = Rfc2898DeriveBytes.Pbkdf2(
            Value,
            Salt,
            Hash_Iterations,
            HashAlgorithmName.SHA256,
            Hash_Size
        );

        // Store salt and hash together so it can be extracted later
        return $"{Convert.ToBase64String(Salt)}:{Convert.ToBase64String(Hash)}";
    }

    /// <param name="Hashed_password">Stored hashed password</param>
    /// <param name="Value">Plain text password</param>
    public static bool Verify_Hashed_Password(string Hashed_password, string Value)
    {
        // get salt and hash
        var Strs = Hashed_password.Split(':');

        if (Strs is null || Strs.Length != 2)
            return false;

        // extract salt bytes
        byte[] Salt = Convert.FromBase64String(Strs[0]);

        // compute hash of val to match
        byte[] Hash = Rfc2898DeriveBytes.Pbkdf2 (
            Value,
            Salt,
            Hash_Iterations,
            HashAlgorithmName.SHA256,
            Hash_Size
        );

        // match with privous hashed password
        return Strs[1] == Convert.ToBase64String(Hash);
    }


    public static byte[] Key_To_Hashed_Bytes(string Key, out bool Completed)
    {
        // transfer load on caller
        Completed = false;
        if (string.IsNullOrEmpty(Key)) return null!;

        Completed = true;
        return SHA256.HashData(Encoding.UTF8.GetBytes(Key));
    }

    public static string Encrypt(string Value, byte[] Hashed_key, out bool Completed)
    {
        // fail safe
        Completed = false;

        try
        {

            if (string.IsNullOrEmpty(Value) || Hashed_key is null || Hashed_key.Length != Hash_Size)
                return "";

            // 3 parts of encryption
            var Text_bytes = Encoding.UTF8.GetBytes(Value); // actuall data
            var Nonce = RandomNumberGenerator.GetBytes(Nonce_Size); // randomizer
            var Tag = new byte[Tag_Size]; // for checking tampering in data
            var Cipher = new byte[Text_bytes.Length]; // scrambeled by AES

            // scramble by aes
            using (var Aes = new AesGcm(Hashed_key, Tag_Size))
            {
                Aes.Encrypt(Nonce, Text_bytes, Cipher, Tag);
            }

            // final arr non then tag then ciper
            var Payload = new byte[Nonce_Size + Tag_Size + Cipher.Length];

            // transfer data
            Buffer.BlockCopy(Nonce, 0, Payload, 0, Nonce_Size);
            Buffer.BlockCopy(Tag, 0, Payload, Nonce_Size, Tag_Size);
            Buffer.BlockCopy(Cipher, 0, Payload, Nonce_Size + Tag_Size, Cipher.Length);

            // done
            var Result = Convert.ToBase64String(Payload);

            Completed = true;
            return Result;
        }
        catch
        {
            return "";
        }
    }
    public static string Decrypt(string Encrypted_str, byte[] Hashed_key, out bool Completed)
    {
        // fail safe
        Completed = false;

        try
        {
            if (string.IsNullOrEmpty(Encrypted_str) || Hashed_key is null || Hashed_key.Length != Hash_Size)
                return "";

            // 3 parts of encryption and actual data are in payload Nonce+tag+cipher
            var Payload = Convert.FromBase64String(Encrypted_str);

            if (Payload.Length <= Nonce_Size + Tag_Size)
                return "";

            // get text len from payload as encrypted text is base 64 not utf8
            var Text_bytes = new byte[Payload.Length - Nonce_Size - Tag_Size];

            // get data into separate parts

            // scramble by aes
            try
            {
                using (var Aes = new AesGcm(Hashed_key, Tag_Size))
                {
                    Aes.Decrypt
                    (
                        Payload.AsSpan(0, Nonce_Size),
                        Payload.AsSpan(Nonce_Size + Tag_Size, Text_bytes.Length),
                        Payload.AsSpan(Nonce_Size, Tag_Size),
                        Text_bytes
                    );
                }
            }
            catch
            {
                return "";
            }

            // done
            var Result = Encoding.UTF8.GetString(Text_bytes);

            Completed = true;
            return Result;
        }
        catch
        {
            return "";
        }
    }

    /// <param name="Id">User id/ email/ username/ anything</param>
    /// <param name="Hahed_key">Same one used by encrypt, hasher which is made by secret key</param>
    /// <param name="Minutes">How long token shal live for, min 30</param>
    /// <returns></returns>
    public static string Gen_JWT_Token(long Id, byte[] Hashed_key, ushort Minutes, out bool Completed)
    {
        // must for preventing crash
        Completed = false;

        if (Hashed_key is null || Hashed_key.Length != Hash_Size || Minutes < 30)
            return "";

        try
        {
            // a claimed id by user who he is
            var User_claim = new Claim(ClaimTypes.NameIdentifier, Id.ToString());

            // used for identification, added user claimed id to it
            var User_identity = new ClaimsIdentity();
            User_identity.AddClaim(User_claim);

            // security key used to reverse the encryption, so ASP can read it
            var Security_key = new SymmetricSecurityKey(Hashed_key);

            // make new security using key and sha
            var Credentials = new SigningCredentials(Security_key, SecurityAlgorithms.HmacSha256Signature);

            // description of token
            var Token_descriptor = new SecurityTokenDescriptor()
            {
                Subject = User_identity,
                Expires = DateTime.UtcNow.AddMinutes(Minutes), // how longvalid for
                SigningCredentials = Credentials
            };

            // create toeken
            var Token = JWT_token_handler.CreateToken(Token_descriptor);

            // finally all process done to make token
            Completed = true;
            return JWT_token_handler.WriteToken(Token);
        }
        catch
        {
            return "";
        }
    }

}
