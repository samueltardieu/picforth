\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\
\ Utilities to manipulate nibbles. Characters can be in lower or upper case,
\ and will be generated as upper case.
\

code nibble>hex ( b -- c )
    \ Add 246 so that C is set for A-F and not for 0-9
    f6 movlw
    indf ,f addwf
    \ movlw won't change status
    3a movlw
    \ If C is set, we have a letter, add 7 extra
    c btfsc
    7 addlw
    \ Add to indf
    indf ,f addwf
    return
end-code

code hex>nibble ( c -- b )
    indf ,w movf
    indf 4 btfss
    9 addlw
    f andlw
    indf movwf
    return
end-code
