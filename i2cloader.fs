\
\ Code for a I2C loader, to reprogram the PIC in software
\
\ Hardware: 16F873SO 26MHz
\
\ This loader is loaded at address 0x600 and uses itself no eeprom. The
\ two first words of the programmed software (a clrf pclath and goto at
\ this time) will be put in 0x602 instead of 0x600.
\
\ If pin 6B is tied to the ground (an internal pullup is used to detect a
\ floating condition), the loader will start at address 7 on the I2C bus.
\ Otherwise, the firmware will be executed by jumping at 0x602.
\

pic16f87x

\ 600 is the origin, 602 is the original jump code for the program

602 org
: firmware-startup ( -- ) nop ;

include libstore.fs
include libfetch.fs
include piceeprom.fs
include picflash.fs

variable i2c-addr
variable i2c-length
create i2c-buffer variable i2c-command create i2c-data $1f allot

\ ----------------------------------------------------------------------
\ Debug led
\ ----------------------------------------------------------------------

5 pin-a debug-led

\ ----------------------------------------------------------------------
\ Commands
\ ----------------------------------------------------------------------

variable idx
variable count

: add-to-buffer ( b -- ) i2c-addr @ ! 1 i2c-addr +! ;

: reset-i2c-buffer ( -- ) i2c-buffer i2c-addr ! ;

\ Copy 16 bytes long string from eeprom

: read-eeprom-data ( src -- )
    reset-i2c-buffer
    i2c-data @ idx !
    $10 count v-for
      idx @ ee@ add-to-buffer 1 idx +!
    v-next
    debug-led toggle
;

: write-eeprom-data ( -- )
    i2c-data 1+ i2c-addr !
    i2c-data @ idx !
    8 count v-for
      i2c-addr @ @ idx @ ee! 1 i2c-addr +! 1 idx +!
    v-next
;

: read-flash-data ( -- )
    reset-i2c-buffer
    i2c-data @ eeadrh !
    i2c-data 1+ @ eeadr !
    $10 count v-for
      flash-read
      eedath @ add-to-buffer
      eedata @ add-to-buffer
      1 eeadr +! z bit-set? if 1 eeadrh +! then
    v-next
;

: write-flash-data ( -- )
    i2c-data @ eeadrh !
    i2c-data 1+ @ eeadr !
    i2c-data 2 + @ eedath !
    i2c-data 3 + @ eedata !
    flash-write
;

: check-loader ( -- ) $55 i2c-buffer ! $aa i2c-buffer 1+ ! ;

\ ----------------------------------------------------------------------
\ I2C
\ ----------------------------------------------------------------------

:: write-i2c ( b -- ) sspbuf ! ckp bit-set ;

: read-i2c ( -- b ) sspbuf @ sspov bit-clr ;

macro
: ack-i2c ( -- ) sspif bit-clr ;
target

\ There has been an incoming request, handle it
: i2c-incoming ( -- )
    \ Commands without parameter
    i2c-length @ 1 = if
      \ 0x06: check-loader
      i2c-command @ $06 = if check-loader exit then
      \ 0x50: reset
      i2c-command @ $50 = if reset then
      \ 0x52: branch to user program
      i2c-command @ $52 = if ]asm 602 goto asm[ then
    then
    \ Commands with one parameter
    i2c-length @ 2 = if
      \ 0x10: read eeprom data
      i2c-command @ $10 = if read-eeprom-data exit then
    then
    \ Commands with two parameters
    i2c-length @ 3 = if
      \ 0x20: read flash data
      i2c-command @ $20 = if read-flash-data exit then
    then
    \ Commands with four parameters
    i2c-length @ 5 = if
      \ 0x21: write flash data
      i2c-command @ $21 = if write-flash-data exit then
    then
    \ Commands with 9 parameters
    i2c-length @ $a = if
      \ 0x11: write eeprom data
      i2c-command @ $11 = if write-eeprom-data exit then
    then
    \ Command is unknown or only partially available
;

\ State 1: make a dummy read and reset reception area
: i2c-state1 ( -- )
    read-i2c drop 0 i2c-length ! reset-i2c-buffer ;

\ State 2: add byte to command
: i2c-state2 ( -- )
    read-i2c i2c-addr @ ! 1 i2c-addr +! 1 i2c-length +! i2c-incoming ;

\ State 4: send next byte
: i2c-state4 ( -- ) i2c-addr @ @ write-i2c 1 i2c-addr +! ;

\ State 3: reset reception area and send first byte
: i2c-state3 ( -- ) reset-i2c-buffer i2c-state4 ;

\ State 5: end of read, nothing to do
: i2c-state5 ( -- ) ;

: handle-i2c ( -- )
    \ Acknowledge i2c
    ack-i2c
    \ Mask out unimportant bits
    sspstat @ $2d and
    \ Write operation, last byte was an address, buffer full
    dup $09 = if drop i2c-state1 exit then
    \ Write operation, last byte was data, buffer full
    dup $29 = if drop i2c-state2 exit then
    \ Read operation, last byte was an address, buffer empty
    dup $0c = if drop i2c-state3 exit then
    \ Read operation, last byte was data, buffer empty
    dup $2c = if drop i2c-state4 exit then
    \ A NACK has been received by the master, logic is reset
    $28 = if i2c-state5 exit then
    \ This is an inconsistent state, do nothing
;
    
\ ----------------------------------------------------------------------
\ Initialization and main program
\ ----------------------------------------------------------------------

\ We add four nops to let some time to the pin to stabilize. The weak
\ internal pullup is very weak, as the name says.

6 pin-b jumper

: no-jumper? ( -- f )
  /rbpu bit-clr jumper >input nop nop nop nop jumper bit-set? /rbpu bit-set ;

: init ( -- )
    debug-led >output debug-led high
    no-jumper? if debug-led low ]asm 602 goto asm[ then
    \ I2C slave mode is used
    $36 sspcon !                \ Enable I2C slave 7bit add., do not hold clock
    7 2* sspadd !               \ Set I2C address
    0 sspstat !                 \ Clear all error condition bits
    0 sspcon2 !                 \ Do not answer general call
;

forth> 600 set-vector

: monitor ( -- ) begin clrwdt sspif bit-set? if handle-i2c then again ;

main : main ( -- ) init monitor ;

\ Configuration word

fosc-hs set-fosc      \ High-speed (26MHz) oscillator
false set-wdte        \ Do not use watchdog timer
false set-lvp         \ No low-voltage programming
false set-boden       \ No brown-out reset
true set-wrt          \ Enable flash-writing
false set-/pwrte      \ Power-up timer
