#ifndef UTF8_FOR_TAGLIB_H
#define UTF8_FOR_TAGLIB_H

#include <iterator>
#include <exception>
#include <string>

namespace utf8 {
    class exception : public std::exception {
    public:
        virtual const char* what() const noexcept override { return "utf8 error"; }
    };

    template <typename octet_iterator, typename u16bit_iterator>
    u16bit_iterator utf8to16(octet_iterator start, octet_iterator end, u16bit_iterator result) {
        while (start != end) {
            unsigned char c = static_cast<unsigned char>(*start++);
            if (c < 0x80) {
                *result++ = static_cast<unsigned short>(c);
            } else if ((c & 0xE0) == 0xC0) {
                if (start == end) break;
                unsigned char c2 = static_cast<unsigned char>(*start++);
                *result++ = static_cast<unsigned short>(((c & 0x1F) << 6) | (c2 & 0x3F));
            } else if ((c & 0xF0) == 0xE0) {
                if (start == end) break;
                unsigned char c2 = static_cast<unsigned char>(*start++);
                if (start == end) break;
                unsigned char c3 = static_cast<unsigned char>(*start++);
                *result++ = static_cast<unsigned short>(((c & 0x0F) << 12) | ((c2 & 0x3F) << 6) | (c3 & 0x3F));
            }
        }
        return result;
    }

    template <typename u16bit_iterator, typename octet_iterator>
    octet_iterator utf16to8(u16bit_iterator start, u16bit_iterator end, octet_iterator result) {
        while (start != end) {
            unsigned short cp = static_cast<unsigned short>(*start++);
            if (cp < 0x80) {
                *result++ = static_cast<unsigned char>(cp);
            } else if (cp < 0x800) {
                *result++ = static_cast<unsigned char>((cp >> 6) | 0xc0);
                *result++ = static_cast<unsigned char>((cp & 0x3f) | 0x80);
            } else {
                *result++ = static_cast<unsigned char>((cp >> 12) | 0xe0);
                *result++ = static_cast<unsigned char>(((cp >> 6) & 0x3f) | 0x80);
                *result++ = static_cast<unsigned char>((cp & 0x3f) | 0x80);
            }
        }
        return result;
    }
}

#endif
