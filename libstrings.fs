\
\ PicForth library file
\
\ This library file has been written by Samuel Tardieu <sam@rfc1149.net>.
\ It belongs to the public domain. Do whatever you want with it.
\
\ Counted strings library: write strings in flash, as 7 bits null-terminated
\ strings.
\
\ The picflash.fs file will be included if it has not
\

needs picflash.fs

variable string-odd

host

: write-packed ( caddr u -- )
  begin
    dup 1 > while
    over dup c@ 7 lshift swap 1+ c@ or csdata, 2 /string
  repeat
  if c@ 7 lshift csdata, else drop 0 csdata, then
;

\ Install a jump over the string data (which is stored in the code) and
\ store the packed data.

: jump-over ( u -- u )
  \ Compute target address
  dup 1+ ( account for zero ) 1+ 2/ ( stored length ) 3 + ( goto ) tcshere +
  \ Set bank
  dup 8 rshift movlw pclath movwf
  \ Jump to this address
  goto reachable ;

: store-packed ( caddr u -- )
  deadcode? @ if exit then
  jump-over
  tcshere >r write-packed
  r> dup 8 rshift (literal) eeadrh (literal) meta> !
  $ff and (literal) eeadr (literal) meta> !
  0 (literal) string-odd (literal) meta> !
  cbank-ok
;

meta

\ Set up a counted string. At runtime, this sets up the eeadrh/eeadr
\ registers so that they point to the beginning of the string.

: c" [char] " parse store-packed ; \ " Keep VIM happy

target

\ This word returns the next character in a counted string (can be zero if
\ the string end has just been reached). It is an iterator; successive calls
\ will return successive characters.

: str-char ( -- c )
  string-odd @ 1 and if 0 string-odd ! eedata @ $7f and exit then
  1 string-odd +!
  flash-read eedath @ 2* eedata @ $80 and if 1 or then
  1 eeadr +! z bit-set? if 1 eeadrh +! then
;
