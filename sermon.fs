\
\ Code for the DCC command station.
\
\ Serial monitor
\
\ An appropriate origin is $1e00 for PIC 16f876 and $e00 for PIC 16f873.
\ Caution: the method for writing flash on 16f87xA devices is different
\ and is not covered by this monitor.
\

$1e00 constant origin

\ Version

4 constant version

\ We let three bytes at origin for jumping to the main program, and 3 bytes
\ for jumping to the firmware.

forth> origin 6 + org

include piceeprom.fs
include libnibble.fs
include libfetch.fs
include libstore.fs
include picflash.fs

\ ----------------------------------------------------------------------
\ LED
\ ----------------------------------------------------------------------

0 pin-a led-green
1 pin-a led-red

\ ----------------------------------------------------------------------
\ Serial port handling
\ ----------------------------------------------------------------------

: emit ( b -- ) begin txif bit-set? until txreg ! ;

macro
: key? ( -- f ) clrwdt rcif bit-set? ;
target
: clear-errors ( -- ) oerr bit-set? if cren bit-clr cren bit-set then ;
: key ( -- b ) begin key? until rcreg @ clear-errors ;

: get8 ( -- b ) key hex>nibble 4 lshift key hex>nibble or ;
:: emit8 ( b -- ) dup 4 rshift nibble>hex emit $f and nibble>hex emit ;

\ ----------------------------------------------------------------------
\ Initialization
\ ----------------------------------------------------------------------

: init ( -- )
    \ LEDs
    $06 adcon1 !                 \ No analog input
    led-green low led-red high
    led-green >output led-red >output
    \ Serial port transmit (Microchip DS30292C page 100)
    portc 6 >output              \ Port C6 is TX
    $14 spbrg !                  \ 57600 bauds with BGRH high at 20MhZ
    \ $19 spbrg !                  \ 9600 bauds with BGRH high at 4MhZ
    $24 txsta !                  \ Asynchronous transmit high-speed generator
    \ Serial port receive (Microchip DS30292C page 102)
    $90 rcsta !                  \ Serial-enable 8bits-rx continuous-receive
    $05 option_reg !             \ CLKOUT, prescaler(tmr0)=64
;

\ ----------------------------------------------------------------------s 
\ Monitor commands
\ ----------------------------------------------------------------------s 

: prompt ( -- ) [char] > emit ;
: ack ( -- ) [char] ! emit ;
: nack ( -- ) [char] ? emit ;

: flash-addr ( -- ) get8 eeadrh ! get8 eeadr ! ;
: mon-fread ( -- ) flash-addr ack flash-read eedath @ emit8 eedata @ emit8 ;
: mon-fwrite ( -- ) flash-addr get8 eedath ! get8 eedata ! ack flash-write ;

: mon-eread ( -- ) get8 ack ee@ emit8 ;
: mon-ewrite ( -- ) get8 get8 ack swap ee! ;

: mon-mread ( -- ) get8 ack @ emit8 ;
: mon-mwrite ( -- ) get8 get8 ack swap ! ;

: mon-exec ( -- ) get8 get8 ack swap pclath ! pcl ! ;

: mon-version ( -- ) ack version emit8 ;

macro
: high-offset ( -- b ) origin 8 rshift ;
: low-offset ( -- b ) origin $ff and 3 + ;
target

: firmware-startup ( -- )
  led-green low led-red low high-offset pclath ! low-offset pcl ! ;

: mon-firmware ( -- ) ack firmware-startup ;

: mon-offset ( -- ) ack high-offset emit8 low-offset emit8 ;

: handle-cmd ( c -- )
  dup [char] f = if drop mon-fread exit then
  dup [char] F = if drop mon-fwrite exit then
  dup [char] e = if drop mon-eread exit then
  dup [char] E = if drop mon-ewrite exit then
  dup [char] m = if drop mon-mread exit then
  dup [char] M = if drop mon-mwrite exit then
  dup [char] X = if drop mon-exec exit then
  dup [char] O = if drop mon-firmware exit then
  dup [char] o = if drop mon-offset exit then
  dup [char] v = if drop mon-version exit then
  drop nack
;

\ ----------------------------------------------------------------------
\ Main program and main loop
\ ----------------------------------------------------------------------

: mainloop ( -- ) led-green low led-red high begin prompt key handle-cmd again ;

\ Timer routines. At 20MhZ, with a prescaler of 64, each tick corresponds
\ to 12.8µs. Call ticks with a "negate"d value. The minimum watchdog time is
\ 7ms; it needs to be cleared within the loop.

:: ticks ( -n -- )
    tmr0 !                                   \ Store -n into tmr0
    t0if bit-clr                             \ Clear overflow bit
    begin clrwdt t0if bit-set? until         \ Wait for overflow to occur
;

: 10ms ( -- ) -$c4 ticks -$c4 ticks -$c4 ticks -$c4 ticks ;

\ If a key is pressed within four seconds, the monitor is started

: 200ms ( -- ) $14 tmp1 v-for 10ms v-next led-green toggle led-red toggle ;
: 4s ( -- ) $14 tmp2 v-for 200ms rcif bit-set? if mainloop exit then v-next ;

\ This fonction lets 4 seconds to the user to enter monitor mode

: start-monitor ( -- ) 4s firmware-startup ;
  
forth> $1e00 set-vector

main : main ( -- ) gie bit-clr clrwdt init start-monitor ;

\ ----------------------------------------------------------------------
\ Configuration word
\ ----------------------------------------------------------------------

fosc-hs set-fosc    \ High-speed oscillator
false set-/pwrte    \ Power-up reset timer
false set-lvp       \ No need for low voltage programming
