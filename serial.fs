host hex

variable serfd
variable serbuf
: seropen ( -- ) s" /dev/cuaa0" r/w open-file throw serfd ! ;
: serclose ( -- ) serfd @ close-file throw ;
: seremit ( c -- ) serbuf c! serbuf 1 serfd @ write-file throw ;
: serkey ( -- c ) serbuf 1 serfd @ read-file throw drop serbuf c@ ;

: nibble>hex ( n -- c ) dup 9 > if $a - [char] A + else [char] 0 + then ;
: hex>nibble ( c -- n ) $ffdf and $10 - dup 9 > if $27 - then ;

: write4 ( n -- ) nibble>hex seremit ;
: write8 ( b -- ) dup 4 rshift write4 $f and write4 ;
: write16 ( w -- ) dup 8 rshift write8 $ff and write8 ;

: read4 ( -- n ) serkey hex>nibble ;
: read8 ( -- b ) read4 4 lshift read4 or ;
: read16 ( -- w ) read8 8 lshift read8 or ;

: need-ack ( -- ) begin serkey [char] ! = until ;
: need-prompt ( -- ) begin serkey [char] > = until ;
: cmd ( char -- ) need-prompt seremit ;

: read-flash ( addr -- value ) [char] f cmd write16 need-ack read16 ;
: write-flash ( value addr -- ) [char] F cmd write16 write16 need-ack ;
: read-eeprom ( addr -- ) [char] e cmd write8 need-ack read8 ;
: write-eeprom ( value addr -- ) [char] E cmd write8 write8 need-ack ;
: read-mem ( addr -- ) [char] m cmd write8 need-ack read8 ;
: write-mem ( value addr -- ) [char] M cmd write8 write8 need-ack ;
: cmd-execute ( addr -- ) [char] X cmd write16 need-ack ;
: cmd-firmware ( -- ) [char] O cmd need-ack ;
: cmd-offset ( -- off ) [char] o cmd need-ack read16 ;

: nochange ( -- ) [char] . emit ;
: change ( -- ) [char] P emit ;

: program-flash ( value addr -- )
  2dup read-flash = if nochange 2drop else change write-flash then ;

: program-eeprom ( value addr -- )
  2dup read-eeprom = if nochange 2drop else change write-eeprom then ;

: open-device ( -- ) seropen [char] ? seremit ;
: close-device ( -- ) serclose ;

: copy-eeprom ( addr -- ) dup tee@ swap program-eeprom ;

true value translate
: no-translation ( -- ) false to translate ;
: fix-address ( addr -- addr' ) translate if dup 4 < if cmd-offset + then then ;
: copy-flash ( addr -- ) dup tcs@ swap fix-address program-flash ;

: copy-all-eeprom ( -- ) teehere 0 ?do i tee-used? if i copy-eeprom then loop ;
: copy-all-flash ( -- ) tcshere 0 ?do i used? if i copy-flash then loop ;

: firmware ( -- ) open-device cmd-firmware close-device ;
: reset ( -- ) open-device 0 cmd-execute close-device ;

: serprog ( -- )
  open-device
  s" Programming FLASH: " type copy-all-flash cr
  s" Programming EEPROM: " type copy-all-eeprom cr
  close-device
;
