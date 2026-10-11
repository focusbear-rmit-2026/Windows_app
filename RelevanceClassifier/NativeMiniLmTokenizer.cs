using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Focus_Bear.RelevanceClassifier
{
    internal class NativeMiniLmTokenizer
    {
        private readonly Dictionary<string, long> _vocabulary;

        private readonly long _clsTokenId;
        private readonly long _sepTokenId;
        private readonly long _padTokenId;
        private readonly long _unkTokenId;

        // all-MiniLM-L6-v2 SentenceTransformer maximum sequence length.
        private const int MaxSequenceLength = 256;

        public int VocabularySize
        {
            get { return _vocabulary.Count; }
        }

        public NativeMiniLmTokenizer(string vocabularyPath)
        {
            if (string.IsNullOrWhiteSpace(vocabularyPath))
                throw new ArgumentException(
                    "Vocabulary path cannot be empty.",
                    nameof(vocabularyPath)
                );

            if (!File.Exists(vocabularyPath))
                throw new FileNotFoundException(
                    "MiniLM vocab.txt not found.",
                    vocabularyPath
                );

            _vocabulary =
                new Dictionary<string, long>(
                    StringComparer.Ordinal
                );

            string[] vocabularyLines =
                File.ReadAllLines(vocabularyPath);

            for (int i = 0; i < vocabularyLines.Length; i++)
            {
                string token = vocabularyLines[i];

                if (!_vocabulary.ContainsKey(token))
                {
                    _vocabulary[token] = i;
                }
            }

            _clsTokenId = GetRequiredTokenId("[CLS]");
            _sepTokenId = GetRequiredTokenId("[SEP]");
            _padTokenId = GetRequiredTokenId("[PAD]");
            _unkTokenId = GetRequiredTokenId("[UNK]");
        }

        public TokenizedInput Encode(string text)
        {
            text = text ?? string.Empty;

            List<string> basicTokens =
                BasicTokenize(text);

            List<long> tokenIds =
                new List<long>();

            // [CLS]
            tokenIds.Add(_clsTokenId);

            foreach (string token in basicTokens)
            {
                List<long> wordPieceIds =
                    WordPieceTokenize(token);

                foreach (long id in wordPieceIds)
                {
                    // Reserve the final position for [SEP].
                    if (tokenIds.Count >= MaxSequenceLength - 1)
                        break;

                    tokenIds.Add(id);
                }

                if (tokenIds.Count >= MaxSequenceLength - 1)
                    break;
            }

            // [SEP]
            tokenIds.Add(_sepTokenId);

            int sequenceLength = tokenIds.Count;

            long[] inputIds =
                new long[sequenceLength];

            long[] attentionMask =
                new long[sequenceLength];

            long[] tokenTypeIds =
                new long[sequenceLength];

            for (int i = 0; i < sequenceLength; i++)
            {
                inputIds[i] = tokenIds[i];

                // Every token in this single-sentence input is active.
                attentionMask[i] = 1;

                // BERT single-sequence segment ID.
                tokenTypeIds[i] = 0;
            }

            return new TokenizedInput(
                inputIds,
                attentionMask,
                tokenTypeIds
            );
        }

        private List<string> BasicTokenize(string text)
        {
            // Match uncased BERT preprocessing:
            // clean text -> Chinese character spacing ->
            // lowercase -> strip accents -> punctuation splitting.

            string cleaned =
                CleanText(text);

            string withChineseSpacing =
                TokenizeChineseCharacters(cleaned);

            string[] originalTokens =
                withChineseSpacing.Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries
                );

            List<string> splitTokens =
                new List<string>();

            foreach (string originalToken in originalTokens)
            {
                string token =
                    originalToken.ToLowerInvariant();

                token =
                    StripAccents(token);

                List<string> punctuationSplit =
                    SplitOnPunctuation(token);

                splitTokens.AddRange(
                    punctuationSplit
                );
            }

            return splitTokens;
        }

        private List<long> WordPieceTokenize(string token)
        {
            List<long> output =
                new List<long>();

            if (string.IsNullOrEmpty(token))
                return output;

            // Matches BERT's max_input_chars_per_word behaviour.
            if (token.Length > 100)
            {
                output.Add(_unkTokenId);
                return output;
            }

            int start = 0;
            bool isBad = false;

            while (start < token.Length)
            {
                int end = token.Length;

                long currentId = -1;

                while (start < end)
                {
                    string piece =
                        token.Substring(
                            start,
                            end - start
                        );

                    if (start > 0)
                    {
                        piece = "##" + piece;
                    }

                    long vocabularyId;

                    if (_vocabulary.TryGetValue(
                        piece,
                        out vocabularyId
                    ))
                    {
                        currentId = vocabularyId;
                        break;
                    }

                    end--;
                }

                if (currentId < 0)
                {
                    isBad = true;
                    break;
                }

                output.Add(currentId);
                start = end;
            }

            if (isBad)
            {
                output.Clear();
                output.Add(_unkTokenId);
            }

            return output;
        }

        private static string CleanText(string text)
        {
            StringBuilder builder =
                new StringBuilder();

            foreach (char character in text)
            {
                int codePoint = character;

                if (codePoint == 0 ||
                    codePoint == 0xFFFD ||
                    IsControl(character))
                {
                    continue;
                }

                if (IsWhitespace(character))
                {
                    builder.Append(' ');
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static string TokenizeChineseCharacters(
            string text)
        {
            StringBuilder builder =
                new StringBuilder();

            foreach (char character in text)
            {
                int codePoint = character;

                if (IsChineseCharacter(codePoint))
                {
                    builder.Append(' ');
                    builder.Append(character);
                    builder.Append(' ');
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static string StripAccents(string text)
        {
            string normalized =
                text.Normalize(
                    NormalizationForm.FormD
                );

            StringBuilder builder =
                new StringBuilder();

            foreach (char character in normalized)
            {
                UnicodeCategory category =
                    CharUnicodeInfo.GetUnicodeCategory(
                        character
                    );

                if (category !=
                    UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder
                .ToString()
                .Normalize(
                    NormalizationForm.FormC
                );
        }

        private static List<string> SplitOnPunctuation(
            string text)
        {
            List<string> output =
                new List<string>();

            StringBuilder current =
                new StringBuilder();

            foreach (char character in text)
            {
                if (IsPunctuation(character))
                {
                    if (current.Length > 0)
                    {
                        output.Add(
                            current.ToString()
                        );

                        current.Clear();
                    }

                    output.Add(
                        character.ToString()
                    );
                }
                else
                {
                    current.Append(character);
                }
            }

            if (current.Length > 0)
            {
                output.Add(
                    current.ToString()
                );
            }

            return output;
        }

        private static bool IsWhitespace(char character)
        {
            return char.IsWhiteSpace(character);
        }

        private static bool IsControl(char character)
        {
            if (character == '\t' ||
                character == '\n' ||
                character == '\r')
            {
                return false;
            }

            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character
                );

            return category ==
                       UnicodeCategory.Control ||
                   category ==
                       UnicodeCategory.Format;
        }

        private static bool IsPunctuation(char character)
        {
            int codePoint = character;

            if ((codePoint >= 33 &&
                 codePoint <= 47) ||
                (codePoint >= 58 &&
                 codePoint <= 64) ||
                (codePoint >= 91 &&
                 codePoint <= 96) ||
                (codePoint >= 123 &&
                 codePoint <= 126))
            {
                return true;
            }

            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character
                );

            return category ==
                       UnicodeCategory.ConnectorPunctuation ||
                   category ==
                       UnicodeCategory.DashPunctuation ||
                   category ==
                       UnicodeCategory.OpenPunctuation ||
                   category ==
                       UnicodeCategory.ClosePunctuation ||
                   category ==
                       UnicodeCategory.InitialQuotePunctuation ||
                   category ==
                       UnicodeCategory.FinalQuotePunctuation ||
                   category ==
                       UnicodeCategory.OtherPunctuation;
        }

        private static bool IsChineseCharacter(
            int codePoint)
        {
            return
                (codePoint >= 0x4E00 &&
                 codePoint <= 0x9FFF) ||
                (codePoint >= 0x3400 &&
                 codePoint <= 0x4DBF) ||
                (codePoint >= 0xF900 &&
                 codePoint <= 0xFAFF);
        }

        private long GetRequiredTokenId(
            string token)
        {
            long tokenId;

            if (!_vocabulary.TryGetValue(
                token,
                out tokenId
            ))
            {
                throw new InvalidOperationException(
                    "Required MiniLM token missing from vocabulary: " +
                    token
                );
            }

            return tokenId;
        }

        internal sealed class TokenizedInput
        {
            public long[] InputIds { get; private set; }

            public long[] AttentionMask { get; private set; }

            public long[] TokenTypeIds { get; private set; }

            public int SequenceLength
            {
                get { return InputIds.Length; }
            }

            public TokenizedInput(
                long[] inputIds,
                long[] attentionMask,
                long[] tokenTypeIds)
            {
                InputIds = inputIds;
                AttentionMask = attentionMask;
                TokenTypeIds = tokenTypeIds;
            }
        }
    }
}