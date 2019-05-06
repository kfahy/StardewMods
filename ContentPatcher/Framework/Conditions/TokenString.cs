using System.Collections.Generic;
using System.Linq;
using System.Text;
using ContentPatcher.Framework.Lexing;
using ContentPatcher.Framework.Lexing.LexTokens;
using ContentPatcher.Framework.Tokens;
using Pathoschild.Stardew.Common.Utilities;

namespace ContentPatcher.Framework.Conditions
{
    /// <summary>A string value optionally containing tokens.</summary>
    internal class TokenString : ITokenString
    {
        /*********
        ** Fields
        *********/
        /// <summary>The lexical tokens parsed from the raw string.</summary>
        private readonly ILexToken[] LexTokens;

        /// <summary>The underlying value for <see cref="Value"/>.</summary>
        private string ValueImpl;

        /// <summary>The underlying value for <see cref="IsReady"/>.</summary>
        private bool IsReadyImpl;


        /*********
        ** Accessors
        *********/
        /// <summary>The raw string without token substitution.</summary>
        public string Raw { get; }

        /// <summary>The tokens used in the string.</summary>
        public HashSet<TokenName> Tokens { get; } = new HashSet<TokenName>();

        /// <summary>The unrecognised tokens in the string.</summary>
        public InvariantHashSet InvalidTokens { get; } = new InvariantHashSet();

        /// <summary>Whether the string contains any tokens (including invalid tokens).</summary>
        public bool HasAnyTokens => this.Tokens.Count > 0 || this.InvalidTokens.Count > 0;

        /// <summary>Whether the token string value may change depending on the context.</summary>
        public bool IsMutable { get; }

        /// <summary>Whether the token string consists of a single token with no surrounding text.</summary>
        public bool IsSingleTokenOnly { get; }

        /// <summary>The string with tokens substituted for the last context update.</summary>
        public string Value => this.ValueImpl;

        /// <summary>Whether all tokens in the value have been replaced.</summary>
        public bool IsReady => this.IsReadyImpl;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="raw">The raw string before token substitution.</param>
        /// <param name="context">The available token context.</param>
        public TokenString(string raw, IContext context)
            : this(
                lexTokens: new Lexer().ParseBits(raw, impliedBraces: false).ToArray(),
                context: context
            )
        { }

        /// <summary>Construct an instance.</summary>
        /// <param name="lexTokens">The lexical tokens parsed from the raw string.</param>
        /// <param name="context">The available token context.</param>
        public TokenString(ILexToken[] lexTokens, IContext context)
        {
            this.LexTokens = lexTokens ?? new ILexToken[0];

            // set raw value
            this.Raw = string.Join("", this.LexTokens.Select(p => p.Text));
            if (string.IsNullOrWhiteSpace(this.Raw))
            {
                this.ValueImpl = this.Raw;
                this.IsReadyImpl = true;
                return;
            }

            // extract tokens
            bool isMutable = false;
            foreach (LexTokenToken lexToken in this.LexTokens.OfType<LexTokenToken>())
            {
                TokenName name = new TokenName(lexToken.Name, lexToken.InputArg?.Text);
                IToken token = context.GetToken(lexToken.Name, enforceContext: false);
                if (token != null)
                {
                    this.Tokens.Add(name);
                    isMutable = isMutable || token.IsMutable;
                }
                else
                    this.InvalidTokens.Add(lexToken.Text);
            }

            // set metadata
            this.IsMutable = isMutable;
            if (!isMutable)
            {
                if (this.InvalidTokens.Any())
                    this.IsReadyImpl = false;
                else
                {
                    this.GetApplied(context, out string finalStr, out bool isReady);
                    this.ValueImpl = finalStr;
                    this.IsReadyImpl = isReady;
                }
            }
            this.IsSingleTokenOnly = this.LexTokens.Length == 1 && this.LexTokens.First().Type == LexTokenType.Token;
        }

        /// <summary>Update the <see cref="Value"/> with the given tokens.</summary>
        /// <param name="context">Provides access to contextual tokens.</param>
        /// <returns>Returns whether the value changed.</returns>
        public bool UpdateContext(IContext context)
        {
            if (!this.IsMutable)
                return false;

            string prevValue = this.Value;
            this.GetApplied(context, out this.ValueImpl, out this.IsReadyImpl);
            return this.Value != prevValue;
        }

        /// <summary>Get the token placeholder names used in the string.</summary>
        public IEnumerable<string> GetContextualTokenNames()
        {
            return this.GetContextualTokenNames(this.LexTokens);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get a new string with tokens substituted.</summary>
        /// <param name="context">Provides access to contextual tokens.</param>
        /// <param name="result">The input string with tokens substituted.</param>
        /// <param name="isReady">Whether all tokens in the <paramref name="result"/> have been replaced.</param>
        private void GetApplied(IContext context, out string result, out bool isReady)
        {
            bool allReplaced = true;
            StringBuilder str = new StringBuilder();
            foreach (ILexToken lexToken in this.LexTokens)
            {
                switch (lexToken)
                {
                    case LexTokenToken lexTokenToken:
                        TokenName name = new TokenName(lexTokenToken.Name, lexTokenToken.InputArg?.Text);
                        IToken token = context.GetToken(lexTokenToken.Name, enforceContext: true);
                        if (token != null)
                            str.Append(token.GetValues(name).FirstOrDefault());
                        else
                        {
                            allReplaced = false;
                            str.Append(lexToken.Text);
                        }
                        break;

                    default:
                        str.Append(lexToken.Text);
                        break;
                }
            }

            result = str.ToString();
            isReady = allReplaced;
        }

        /// <summary>Get the token placeholder names from the given lexical tokens.</summary>
        /// <param name="lexTokens">The lexical tokens to scan.</param>
        private IEnumerable<string> GetContextualTokenNames(ILexToken[] lexTokens)
        {
            if (lexTokens?.Any() != true)
                yield break;

            foreach (ILexToken lexToken in lexTokens)
            {
                if (lexToken is LexTokenToken token)
                {
                    yield return token.Name;

                    ILexToken[] inputLexTokens = token.InputArg?.Parts;
                    if (inputLexTokens != null)
                    {
                        foreach (string name in this.GetContextualTokenNames(inputLexTokens))
                            yield return name;
                    }
                }
            }
        }
    }
}
