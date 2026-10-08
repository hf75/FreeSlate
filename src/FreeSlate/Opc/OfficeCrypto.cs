using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Opc;

/// <summary>A password-protected document that cannot be opened: no password, a wrong one or an unsupported scheme.</summary>
public sealed class PasswordException(string message, bool wrongPassword = false) : Exception(message)
{
    /// <summary>True when a password was given but does not open the document.</summary>
    public bool WrongPassword { get; } = wrongPassword;
}

/// <summary>
/// Decryption of password-protected Office Open XML documents ([MS-OFFCRYPTO] 2.3.4), catalog: package.encryption.
/// The document is a compound file whose EncryptionInfo stream describes the scheme and whose EncryptedPackage
/// stream holds the encrypted ZIP package after its 8-byte length. Agile encryption (version 4.4, an XML
/// descriptor) spins the password hash (SHA-1 … SHA-512) and unseals a random package key with it, then decrypts
/// the package in 4096-byte segments with AES-CBC, each with its own initialization vector. Standard encryption
/// (version x.2) derives an AES key from 50 000 SHA-1 spins and decrypts the package with AES-ECB.
/// </summary>
public static class OfficeCrypto
{
    /// <summary>The password Office uses for documents protected without one.</summary>
    const string DefaultPassword = "VelvetSweatshop";

    static readonly byte[] VerifierInputBlock = [0xfe, 0xa7, 0xd2, 0x76, 0x3b, 0x4b, 0x9e, 0x79];
    static readonly byte[] VerifierValueBlock = [0xd7, 0xaa, 0x0f, 0x6d, 0x30, 0x61, 0x34, 0x4e];
    static readonly byte[] KeyValueBlock = [0x14, 0x6e, 0x0b, 0xe7, 0xab, 0xac, 0xd0, 0xd6];

    /// <summary>Whether a compound file is an encrypted Office Open XML document.</summary>
    public static bool IsEncrypted(CompoundFile file) =>
        file.Streams.Contains("EncryptionInfo", StringComparer.OrdinalIgnoreCase) && file.Streams.Contains("EncryptedPackage", StringComparer.OrdinalIgnoreCase);

    /// <summary>The decrypted package (ZIP bytes); without a password the default one is tried.</summary>
    public static byte[] Decrypt(CompoundFile file, string? password)
    {
        var info = file.Read("EncryptionInfo") ?? throw new InvalidDataException("The document has no EncryptionInfo stream.");
        var package = file.Read("EncryptedPackage") ?? throw new InvalidDataException("The document has no EncryptedPackage stream.");
        if (info.Length < 8 || package.Length < 8)
            throw new InvalidDataException("The encrypted document is truncated.");
        var (major, minor) = (BinaryPrimitives.ReadUInt16LittleEndian(info), BinaryPrimitives.ReadUInt16LittleEndian(info.AsSpan(2)));
        Func<string, byte[]?> open = (major, minor) switch
        {
            (4, 4) => p => Agile(info, package, p),
            (2 or 3 or 4, 2) => p => Standard(info, package, p),
            _ => throw new PasswordException($"The document uses an unsupported encryption (version {major}.{minor})."),
        };
        if (password == null)
            return open(DefaultPassword) ?? throw new PasswordException("The document is protected by a password.");
        return open(password) ?? throw new PasswordException("The password does not open the document.", wrongPassword: true);
    }

    static byte[]? Agile(byte[] info, byte[] package, string password)
    {
        XNamespace e = "http://schemas.microsoft.com/office/2006/encryption";
        XNamespace p = "http://schemas.microsoft.com/office/2006/keyEncryptor/password";
        var xml = XDocument.Parse(Encoding.UTF8.GetString(info, 8, info.Length - 8).Trim('﻿', '\0', ' ', '\r', '\n'));
        var keyData = xml.Root?.Element(e + "keyData") ?? throw new InvalidDataException("The encryption descriptor has no keyData.");
        var encryptedKey = xml.Root.Descendants(p + "encryptedKey").FirstOrDefault()
                           ?? throw new PasswordException("The document is encrypted for certificates, not with a password.");

        // The password hash: H0 = H(salt + password), then spinCount rounds of H(round + H).
        var hash = Hash(encryptedKey.Str("hashAlgorithm"));
        var salt = Bytes(encryptedKey, "saltValue");
        var keyBytes = Int(encryptedKey, "keyBits") / 8;
        var blockSize = Int(encryptedKey, "blockSize");
        var h = hash([.. salt, .. Encoding.Unicode.GetBytes(password)]);
        var round = new byte[4 + h.Length];
        for (var i = 0; i < Int(encryptedKey, "spinCount"); i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(round, (uint)i);
            h.CopyTo(round, 4);
            h = hash(round);
        }
        byte[] Key(byte[] block) => Fit(hash([.. h, .. block]), keyBytes);
        var cipher = Cipher(encryptedKey);
        var iv = Fit(salt, blockSize);

        // The verifier: a random input sealed with one key, its hash with another; a wrong password mismatches.
        var input = cipher(Key(VerifierInputBlock), iv, Bytes(encryptedKey, "encryptedVerifierHashInput"));
        var expected = cipher(Key(VerifierValueBlock), iv, Bytes(encryptedKey, "encryptedVerifierHashValue"));
        var inputHash = hash(input.AsSpan(0, Math.Min(input.Length, salt.Length)).ToArray());
        if (expected.Length < inputHash.Length || !expected.AsSpan(0, inputHash.Length).SequenceEqual(inputHash))
            return null;
        var secret = cipher(Key(KeyValueBlock), iv, Bytes(encryptedKey, "encryptedKeyValue"))[..keyBytes];

        // The package: segments of 4096 bytes, each with the IV H(keyData salt + segment number).
        var dataHash = Hash(keyData.Str("hashAlgorithm"));
        var dataSalt = Bytes(keyData, "saltValue");
        var dataBlock = Int(keyData, "blockSize");
        var dataCipher = Cipher(keyData);
        var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(package);
        var output = new byte[Math.Min(size, (package.Length - 8L) / dataBlock * dataBlock)];
        var segmentNumber = new byte[4];
        for (var (segment, offset) = (0, 8); offset < package.Length && segment * 4096L < output.Length; segment++, offset += 4096)
        {
            var count = Math.Min(4096, package.Length - offset);
            count -= count % dataBlock;
            if (count <= 0)
                break;
            BinaryPrimitives.WriteUInt32LittleEndian(segmentNumber, (uint)segment);
            var plain = dataCipher(secret, Fit(dataHash([.. dataSalt, .. segmentNumber]), dataBlock), package.AsSpan(offset, count).ToArray());
            Array.Copy(plain, 0, output, segment * 4096L, Math.Min(plain.Length, output.Length - segment * 4096L));
        }
        return output;
    }

    static byte[]? Standard(byte[] info, byte[] package, string password)
    {
        // Version (4 bytes), flags (4), header size (4), the header, then the verifier.
        var headerSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(8));
        var header = info.AsSpan(12);
        var algorithm = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        var keyBits = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        if (algorithm is not (0x660E or 0x660F or 0x6610))
            throw new PasswordException("The document uses an unsupported cipher (only AES).");
        if (keyBits == 0)
            keyBits = algorithm switch { 0x660F => 192, 0x6610 => 256, _ => 128 };
        var verifier = info.AsSpan(12 + headerSize);
        var saltSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(verifier);
        var salt = verifier.Slice(4, saltSize).ToArray();
        var encryptedVerifier = verifier.Slice(4 + saltSize, 16).ToArray();
        var encryptedVerifierHash = verifier.Slice(4 + saltSize + 20, 32).ToArray();

        // The key: 50 000 SHA-1 spins, the block number 0, then the 0x36 and 0x5C pads ([MS-OFFCRYPTO] 2.3.4.7).
        var h = SHA1.HashData([.. salt, .. Encoding.Unicode.GetBytes(password)]);
        var round = new byte[24];
        for (var i = 0; i < 50000; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(round, (uint)i);
            h.CopyTo(round, 4);
            h = SHA1.HashData(round);
        }
        h = SHA1.HashData([.. h, 0, 0, 0, 0]);
        byte[] Pad(byte value)
        {
            var buffer = Enumerable.Repeat(value, 64).ToArray();
            for (var i = 0; i < h.Length; i++)
                buffer[i] ^= h[i];
            return SHA1.HashData(buffer);
        }
        var key = Pad(0x36).Concat(Pad(0x5C)).Take(keyBits / 8).ToArray();

        using var aes = Aes.Create();
        aes.Key = key;
        var plainVerifier = aes.DecryptEcb(encryptedVerifier, PaddingMode.None);
        var verifierHash = aes.DecryptEcb(encryptedVerifierHash, PaddingMode.None);
        if (!SHA1.HashData(plainVerifier).AsSpan().SequenceEqual(verifierHash.AsSpan(0, 20)))
            return null;
        var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(package);
        var length = (package.Length - 8) / 16 * 16;
        var plain = aes.DecryptEcb(package.AsSpan(8, length), PaddingMode.None);
        return plain[..(int)Math.Min(size, plain.Length)];
    }

    /// <summary>The hash function of an agile descriptor's hashAlgorithm.</summary>
    static Func<byte[], byte[]> Hash(string? name) => name?.ToUpperInvariant() switch
    {
        "SHA1" or "SHA-1" => SHA1.HashData,
        "SHA256" or "SHA-256" => SHA256.HashData,
        "SHA384" or "SHA-384" => SHA384.HashData,
        "SHA512" or "SHA-512" => SHA512.HashData,
        "MD5" => MD5.HashData,
        _ => throw new PasswordException($"The document uses an unsupported hash algorithm ({name})."),
    };

    /// <summary>The block cipher of an agile descriptor: AES in CBC or CFB mode, without padding.</summary>
    static Func<byte[], byte[], byte[], byte[]> Cipher(XElement parameters)
    {
        if (!string.Equals(parameters.Str("cipherAlgorithm"), "AES", StringComparison.OrdinalIgnoreCase))
            throw new PasswordException($"The document uses an unsupported cipher ({parameters.Str("cipherAlgorithm")}).");
        var cfb = parameters.Str("cipherChaining") == "ChainingModeCFB";
        return (key, iv, data) =>
        {
            using var aes = Aes.Create();
            aes.Key = key;
            data = data.Length % 16 == 0 ? data : data[..(data.Length / 16 * 16)];
            return cfb ? aes.DecryptCfb(data, iv, PaddingMode.None, 8) : aes.DecryptCbc(data, iv, PaddingMode.None);
        };
    }

    /// <summary>Bytes cut or padded with 0x36 to the given length ([MS-OFFCRYPTO] 2.3.4.11).</summary>
    static byte[] Fit(byte[] value, int length)
    {
        if (value.Length >= length)
            return value[..length];
        var result = Enumerable.Repeat((byte)0x36, length).ToArray();
        value.CopyTo(result, 0);
        return result;
    }

    static byte[] Bytes(XElement element, string attribute) =>
        Convert.FromBase64String(element.Str(attribute) ?? throw new InvalidDataException($"The encryption descriptor has no {attribute}."));

    static int Int(XElement element, string attribute) =>
        int.TryParse(element.Str(attribute), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"The encryption descriptor has no {attribute}.");
}
