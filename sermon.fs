\ PIC Serial monitor
\
\ Written by Samuel Tardieu (PicForth author)
\
\ Slight mods by David McNab (david@freenet.org.nz):
\  - tidy-ups
\  - added $30 to suggested ORGs (48 bytes on PIC are precious!)
\  - changed vector allocations to 4 words each (to suit 16F87xA
\    processors, which can only flash-write in blocks of 4)

\ -----------------------------------------
\ START USER-CONFIGURATION SETTINGS

\ uncomment one of these according to your processor

$e30 constant origin     \ suits PIC 16F873[A] and 16F88
\ $1e30 constant origin    \ suits PIC 16F876/7[A]

\ uncomment one of these to choose a baudrate
\ (for more options, refer to the Datasheet, chapter 10, USART, table 10-4)

\ $81 constant baudrate  \ 9600 baud with 20MHz oscillator
\ $40 constant baudrate  \ 19200 baud with 20MhZ oscillator
\ $4a constant baudrate  \ 28800 baud with 20MHz oscillator
\ $24 constant baudrate  \ 33600 baud with 20MHz oscillator
$14 constant baudrate  \ 57600 baud with 20MhZ oscillator
\ $19 constant baudrate  \ 9600 baud with 4MHz oscillator
\ $0c constant baudrate  \ 19200 baud with 4MHz oscillator

\ comment one or both of these out depending on your processor

pic16f87x
\ pic16f88

\ chip configs - comment out any you don't want

fosc-hs set-fosc    \ High-speed oscillator
false set-/pwrte    \ Power-up reset timer
false set-lvp       \ No need for low voltage programming
false set-wdte

\ END USER-CONFIGURATION SETTINGS
\ ----------------------------------------------

\ Version

5 constant version

\ We let 4 bytes at origin for jumping to the main program, and 4 bytes
\ for jumping to the firmware.

forth> origin 8 + org

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
    baudrate spbrg !             \ set baudrate (see above)
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
: emit= [char] = emit ;
: emit: [char] : emit ;

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
: low-offset ( -- b ) origin $ff and 4 + ;
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
  
forth> origin set-vector

main : main ( -- ) gie bit-clr clrwdt init start-monitor ;

