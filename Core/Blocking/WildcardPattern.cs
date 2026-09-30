using System;
using System.Collections.Generic;

namespace FocusBear.Core.Blocking
{
    /// <summary>
    /// The Mac app's global <c>wildcard(_ string: String, pattern: String)</c>, which is
    /// <c>NSPredicate(format: "self LIKE %@", pattern)</c>.
    ///
    /// Every block-list and allow-list entry a user types is fed through here, so its exact
    /// semantics decide what gets blocked. They are NOT SQL <c>LIKE</c> and not a regex — verified
    /// against Foundation on macOS 26 rather than inferred from the docs:
    ///
    /// <list type="bullet">
    /// <item>The whole string must match; there is no implicit "contains".</item>
    /// <item><c>*</c> matches zero or more characters, <c>?</c> matches exactly one.</item>
    /// <item><c>\</c> escapes the next character, so <c>\*</c> is a literal asterisk.</item>
    /// <item>Every other character — <c>.</c> <c>+</c> <c>[</c> <c>(</c> <c>%</c> <c>_</c> — is
    ///       literal. A pattern is not a regex, so a "." in a domain cannot match anything else.</item>
    /// <item>Matching is case-sensitive and diacritic-sensitive. Callers lower-case both sides
    ///       first — except one <c>file://</c> path in <see cref="UrlValidation"/>, which does not.</item>
    /// <item>An EMPTY pattern matches nothing at all, not even the empty string. The callers never
    ///       pass one because they wrap entries in <c>*</c>…<c>*</c> first, which turns an empty
    ///       block-list entry into <c>**</c> — the footgun that matches every URL.</item>
    /// </list>
    /// </summary>
    public static class WildcardPattern
    {
        /// <summary>Matches zero or more characters.</summary>
        public const char AnySequence = '*';

        /// <summary>Matches exactly one character.</summary>
        public const char AnyCharacter = '?';

        /// <summary>Makes the following character literal.</summary>
        public const char Escape = '\\';

        /// <summary>
        /// Whether <paramref name="value"/> matches <paramref name="pattern"/> in full.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The pattern ends in an unescaped <see cref="Escape"/>. Foundation raises
        /// <c>NSInvalidArgumentException</c> ("The backslash is a wildcard char, and cannot appear
        /// unescaped at the end of a string") for this, which on the Mac terminates the app — so it
        /// is surfaced rather than quietly treated as a non-match. In practice the callers append a
        /// trailing <c>*</c> to user input before it reaches here, which is what has kept a
        /// block-list entry ending in a backslash from crashing the Mac app.
        /// </exception>
        public static bool Matches(string value, string pattern)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            List<PatternToken> tokens = Compile(pattern);
            if (tokens.Count == 0) return false;
            return MatchesTokens(value, tokens);
        }

        private enum TokenKind { Literal, AnyCharacter, AnySequence }

        private struct PatternToken
        {
            public TokenKind Kind;
            public char Literal;
        }

        private static List<PatternToken> Compile(string pattern)
        {
            var tokens = new List<PatternToken>(pattern.Length);
            for (int index = 0; index < pattern.Length; index++)
            {
                char current = pattern[index];
                if (current == Escape)
                {
                    if (index + 1 >= pattern.Length)
                    {
                        throw new ArgumentException(
                            "The backslash is a wildcard char, and cannot appear unescaped at the end of a string.",
                            nameof(pattern));
                    }
                    index++;
                    tokens.Add(new PatternToken { Kind = TokenKind.Literal, Literal = pattern[index] });
                    continue;
                }
                if (current == AnySequence) { tokens.Add(new PatternToken { Kind = TokenKind.AnySequence }); continue; }
                if (current == AnyCharacter) { tokens.Add(new PatternToken { Kind = TokenKind.AnyCharacter }); continue; }
                tokens.Add(new PatternToken { Kind = TokenKind.Literal, Literal = current });
            }
            return tokens;
        }

        /// <summary>
        /// Greedy match with backtracking to the most recent <c>*</c>. Linear in practice and, unlike
        /// a translated regex, immune to catastrophic backtracking on an entry such as "*a*a*a*a*".
        /// </summary>
        private static bool MatchesTokens(string value, List<PatternToken> tokens)
        {
            int valueIndex = 0;
            int tokenIndex = 0;
            int lastSequenceToken = -1;
            int lastSequenceValue = 0;

            while (valueIndex < value.Length)
            {
                if (tokenIndex < tokens.Count)
                {
                    PatternToken token = tokens[tokenIndex];
                    if (token.Kind == TokenKind.AnySequence)
                    {
                        lastSequenceToken = tokenIndex;
                        lastSequenceValue = valueIndex;
                        tokenIndex++;
                        continue;
                    }
                    if (token.Kind == TokenKind.AnyCharacter
                        || (token.Kind == TokenKind.Literal && token.Literal == value[valueIndex]))
                    {
                        tokenIndex++;
                        valueIndex++;
                        continue;
                    }
                }

                if (lastSequenceToken < 0) return false;
                lastSequenceValue++;
                tokenIndex = lastSequenceToken + 1;
                valueIndex = lastSequenceValue;
            }

            while (tokenIndex < tokens.Count && tokens[tokenIndex].Kind == TokenKind.AnySequence) tokenIndex++;
            return tokenIndex == tokens.Count;
        }
    }
}
