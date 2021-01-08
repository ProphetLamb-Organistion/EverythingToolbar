using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace EverythingToolbar
{
    public sealed class ArgumentParser : IEnumerable<string>
    {
        private readonly string _source;

        private ArgumentParser(string source) => _source = source ?? String.Empty;

        public IEnumerator<string> GetEnumerator() => ParseSource().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static ArgumentParser Parse(string argumentsSource)
        {
            if (String.IsNullOrWhiteSpace(argumentsSource))
                throw new ArgumentException(nameof(argumentsSource));
            return new ArgumentParser(argumentsSource);
        }

        private IEnumerable<string> ParseSource()
        {
            bool previousIsBackslash = false;
            bool inQuoteBlock = false;
            StringBuilder _buffer = new StringBuilder(128);
            for(int i = 0; i < _source.Length; i++)
            {
                switch(_source[i])
                {
                    case ' ':
                        if (inQuoteBlock)
                        {
                            _buffer.Append(' ');
                        }
                        else if (_buffer.Length > 0)
                        {
                            yield return _buffer.ToString();
                            _buffer.Clear();
                        }
                    break;
                    case '"':
                        if (previousIsBackslash)
                        {
                            _buffer.Append('"');
                        }
                        else
                        {
                            if (_buffer.Length > 0)
                            {
                                yield return _buffer.ToString();
                                _buffer.Clear();
                            }
                            if (inQuoteBlock)
                            {
                                inQuoteBlock = false;
                            }
                            else
                            {
                                inQuoteBlock = true;
                                _buffer.Append('"');
                            }
                        }
                    break;
                    case '\\':
                        if (previousIsBackslash)
                        {
                            _buffer.Append('\\');
                        }
                        else
                        {
                            previousIsBackslash = true;
                            continue;
                        }
                    break;
                    case 'n':
                        _buffer.Append(previousIsBackslash ? '\n' : 'n');
                    break;
                    case 'r':
                        _buffer.Append(previousIsBackslash ? '\r' : 'r');
                    break;
                    case 't':
                        _buffer.Append(previousIsBackslash ? '\t' : 't');
                    break;
                    default:
                        _buffer.Append(_source[i]);
                    break;
                }
                previousIsBackslash = false;
            }
        }
    }
}