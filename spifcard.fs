\
\ Code for the SPIF smartcard controller, programmable baud rate generator
\ and bq2010 interface.
\
\ Hardware: 16F873SO 26MHz (overclocked, should be 20MHz)
\

include picisr.fs
include libstore.fs
include libfetch.fs
include libcmove.fs
include piceeprom.fs

variable i2c-length
create i2c-buffer variable i2c-command create i2c-data $20 allot
variable i2c-addr

eecreate i2c-address 7 ee,               \ Default I2C device address

\ ----------------------------------------------------------------------
\ Version information
\ ----------------------------------------------------------------------

1 constant vsn-major
8 constant vsn-minor

\ ----------------------------------------------------------------------
\ Led
\ ----------------------------------------------------------------------

5 pin-a led
variable blinking-counter
variable blinking-speed

: led-on ( -- ) 0 blinking-speed ! led high ;
: led-off ( -- ) 0 blinking-speed ! led low ;
\ Low bit of blinking speed is set to keep the test shorter in the isr
:: led-blink-n ( n -- ) 1 or blinking-speed ! 1 blinking-counter ! ;
: led-blink ( -- ) 3 led-blink-n ;
: led-blink-fast ( -- ) 1 led-blink-n ;

\ ----------------------------------------------------------------------
\ Statistics and error flags
\ ----------------------------------------------------------------------

bank1
variable i2c-overflow
variable sc-parityerrors
variable sc-nack
bank0
variable sc-tocard
variable sc-fromcard

: reset-stats ( -- )
    0 i2c-overflow ! 0 sc-parityerrors ! 0 sc-nack !
    0 sc-tocard ! 0 sc-fromcard ! ;

\ ----------------------------------------------------------------------
\ Timer routines
\ ----------------------------------------------------------------------

\ With a prescaler of 8, each tick takes 1.231탎

:: timer-wait ( n -- )
    tmr0 ! t0if bit-clr                                     \ Setup timer 0
    begin t0if bit-set? until ;                             \ Wait for overflow

:: timer-loop ( n -- ) tmp1 v-for 0 timer-wait v-next ;     \ 315.14탎

: 140탎 ( -- ) -72 timer-wait ;
: 80ms ( -- ) 100 timer-loop ;

\ ----------------------------------------------------------------------
\ Watchdog
\ ----------------------------------------------------------------------

\ The internal watchdog is too fast (we cannot use its postscaler as
\ it is shared with timer 0 prescaler that we use). Build our own here.

variable watchdog-timeout

: clear-watchdog ( -- ) 64 watchdog-timeout ! ;    \ Around 2 seconds

\ ----------------------------------------------------------------------
\ Sleep mode
\ ----------------------------------------------------------------------

: sleep-mode ( -- )
  suspend-interrupts                         \ We do not want a real interrupt
  peie bit-set sspie bit-set                 \ Enable I2C interrupts
  tmr1ie bit-clr                             \ Disable timer 1 interrupts
  begin sleep sspif bit-set? until           \ Wakeup on I2C only
  peie bit-clr sspie bit-clr                 \ Disable I2C interrupts
  tmr1ie bit-set                             \ Enable timer 1 interrupts
  restore-interrupts                         \ Restore interrupts
;

\ ----------------------------------------------------------------------
\ Single wire communication with bq2010
\ ----------------------------------------------------------------------

1 pin-a bq-data

: bq-low ( -- ) bq-data low bq-data >output ;         \ Low output state
: bq-high ( -- ) bq-data >input ;                     \ Input state

\ Timing names come from the bq2010 datasheet

: tb $a timer-loop ;
: tbr 4 timer-loop ;
: tstrh -1 timer-wait ;
: tdv 5 timer-loop ;
: tsv $a timer-loop ;
: tstrb 2 timer-loop ;

: break ( -- ) bq-low tb bq-high tbr ;

: bit-one ( -- ) bq-high tdv ;
: bit-zero ( -- ) tdv bq-high ;

: send-lsb ( b -- b ' ) rrf-tos c bit-set? if bit-one exit then bit-zero ;

variable current-bit
: bq-write-byte ( b -- )
    8 current-bit v-for bq-low tstrh send-lsb tsv v-next drop ;

: arm-timer ( -- ) 0 tmr0 ! t0if bit-clr ;
: wait-for-low ( -- )
    100 tmp1 v-for
      arm-timer begin bq-data low? if exit then t0if bit-set? until
    v-next ;
: wait-for-high ( -- )
    100 tmp1 v-for
      arm-timer begin bq-data high? if exit then t0if bit-set? until
    v-next ;

: get-lsb ( b -- b' )
    wait-for-low tstrb 1 rshift bq-data high? if 80 or then wait-for-high ;
: bq-read-byte ( -- b ) 0 8 current-bit v-for get-lsb v-next ;

: bq! ( b addr -- ) break 80 or bq-write-byte bq-write-byte ;
: bq@ ( addr -- b ) break bq-write-byte bq-read-byte ;

\ ----------------------------------------------------------------------
\ Version and information strings
\ ----------------------------------------------------------------------

variable vsn-count
variable vsn-src
variable vsn-dst

\ Copy 32 bytes long string from eeprom to i2c-buffer
:: i2c-buffer-copy ( src -- )
  vsn-src !
  i2c-buffer vsn-dst !
  $20 vsn-count v-for
    vsn-src @ ee@ vsn-dst @ ! 1 vsn-src +! 1 vsn-dst +!
  v-next
;

: i2c-get-vsn ( -- )
  s" SPIF-PIC 0.332 (PicForth 0.27)  "
  drop i2c-buffer-copy ;

: i2c-get-copyright ( -- )
  s" Copyright (c) 2002 ENST         "
  drop i2c-buffer-copy ;  

\ ----------------------------------------------------------------------
\ Private memory area
\ ----------------------------------------------------------------------

\ The area between 96 and 127 (32 bytes) is reserved for SPIF card
\ identification. The PIC is tied to the card as the flash memory used
\ by the main processor is located on a removable card, so the PIC is
\ a better choice. Bytes are read in a big chunk, and written one at a
\ time.

$60 constant private

: i2c-get-private ( -- ) private i2c-buffer-copy ;
: i2c-private-write ( -- ) i2c-data 1+ @ i2c-data @ private + ee! ;

\ ----------------------------------------------------------------------
\ Event signalling
\ ----------------------------------------------------------------------

4 pin-a interrupt
variable event
1 constant startup
2 constant smartcard
4 constant i2c-error

\ A pulse is generated for the main processor core; setting the interrupt
\ line low, then in input mode again, meets the other processor timing
\ expectations by a large factor (the pulse is maintained for more than 150ns).

:: signal-event ( n -- )
   event or! interrupt low interrupt >output interrupt >input ;

\ ----------------------------------------------------------------------
\ Analog to digital conversion
\ ----------------------------------------------------------------------

: i2c-adc ( -- )
    go//done bit-set begin go//done bit-clr? until
    adresh @ i2c-buffer ! adresl @ i2c-buffer 1+ !
;

\ ----------------------------------------------------------------------
\ Smartcard
\ ----------------------------------------------------------------------

\ The smartcard is access through a Philips TDA8004 device, which takes
\ care of proper power-up and power-down sequences.

0 pin-b i/o
1 pin-b clkdiv1
2 pin-b clkdiv2
3 pin-b aux1
4 pin-b aux2
5 pin-b /off

0 pin-c rstin
1 pin-c /cmdvcc

variable atr-length         \ ATR length and ATR must be contiguous
create atr $10 allot        \ Max ATR length is 16

variable sc-prev            \ Zero if no card is present
variable sc-count
variable sc-addr
variable sc-inverse
variable sc-parity
variable sc-max-length

\ Helper routines

macro
: sc-addr++ ( -- ) 1 sc-addr +! ;
target
: sc-addr@++ ( -- b ) sc-addr @ @ sc-addr++ ;
: sc-addr!++ ( b -- ) sc-addr @ ! sc-addr++ ;

\ The card reader frequency is 3.25MHz (26MHz/8). Each bit takes 372 clock
\ pulses, which means 114.46탎, i.e. around 93 times 1.231탎.

: delay-bit ( -- ) -5d timer-wait ;
: delay-half-bit ( -- ) -2e timer-wait ;

\ The card can use up to 9600 bits (i.e. around one second) before
\ starting the next byte. Each time it is possible to deduce the right
\ size, it should be used to avoid useless waiting time. If a start
\ bit is found, a delay of half a bit is spent before returning.

: next-start-bit? ( -- f )
  i/o >input
  60 begin
    dup while
    64 begin
      dup while
      0 tmr0 ! t0if bit-clr
      begin
        i/o low? if 2drop true delay-half-bit exit then
      t0if bit-set? until
    1- repeat drop
  1- repeat drop false
;

\ Toggle parity

: toggle-parity ( -- ) 1 sc-parity xor! ;

\ Smartcards can be operated in two modes: direct convention uses high-level
\ ones with LSB transmitted first, while inverse convention uses low-level
\ ones with MSB transmitted first. In both cases, even parity is used.

: get-bit-inverse ( b -- b' )
  1 lshift i/o high? if exit then 1 or toggle-parity ;
: get-bit-direct ( b -- b' )
  1 rshift i/o low? if exit then 80 or toggle-parity ;
: get-bit ( b -- b' )
  sc-inverse @ if get-bit-inverse exit then get-bit-direct ;

\ Signal a parity error

: parity-error ( -- )
  1 sc-parityerrors +!                                     \ Count errors
  delay-bit i/o low i/o >output delay-bit delay-bit ;      \ Signal error

\ Return a byte and 1 if a byte was sent by the smartcard and correctly
\ received (maybe after several attempts), or 0 if no byte could be read.
\ It also increment the number of bytes received by the card

: get-byte? ( -- b true | false )
  next-start-bit? 0= if false exit then                     \ No start bit
  0 sc-parity ! 0 8 sc-count v-for delay-bit get-bit v-next \ Get 8 bits
  delay-bit i/o low? if toggle-parity then                  \ Count parity
  sc-parity @ 1 and if drop parity-error recurse exit then  \ Retry
  delay-bit 1 sc-fromcard +! true ;                         \ Count byte

\ Get ACK or SW1 from card

: ack-or-sw1? ( -- b true | false )
  get-byte? 0= if false exit then                           \ Mute card
  dup $60 = if drop recurse exit then                       \ Extra delay
  true ;

\ Test whether the received answer is a SW1 or not

: =ins? ( b -- f ) 1 /and i2c-data 3 + @ = ;                \ INS or INS+1

: is-sw1? ( b -- f )
  dup =ins? if drop true exit then                          \ ACK
  invert =ins? ;                                            \ Inverse ACK

: send-bit-inverse ( b -- b' )
  rlf-tos c bit-set? if i/o low toggle-parity exit then i/o high ;
: send-bit-direct ( b -- b' )
  rrf-tos c bit-set? if i/o high exit then i/o low toggle-parity ;
: send-bit ( b -- b' )
  sc-inverse @ if send-bit-inverse exit then send-bit-direct ;

\ Send byte and first stop bit (handle parity error from the card)

: send-byte ( b -- )
  0 sc-parity !
  dup                                                            \ Copy byte
  i/o low i/o >output delay-bit                                  \ Start bit
  8 sc-count v-for send-bit delay-bit v-next drop                \ Send bits
  sc-parity @ 1 and if i/o low else i/o high then delay-bit      \ Send parity
  i/o >input delay-bit                                           \ 1st stop bit
  i/o low? if 1 sc-nack +! delay-bit delay-bit recurse exit then \ Parity error?
  drop 1 sc-tocard +!                                            \ Byte sent
;

\ Send next byte to the card, as well as the first stop bit

: send-next-byte ( -- ) sc-addr@++ send-byte ;

\ Send a bunch of bytes to the card in a row and the first stop bit

: send-batch ( length -- )
  begin send-next-byte 1- dup while delay-bit repeat drop ;

\ Send remaining arguments

: send-remaining ( length -- SW1 true | false )
  dup if send-batch else drop then get-byte? ;

\ Send arguments to the card, according to ISO 7816-3. Return
\ SW1 and true if everything went well, and false alone
\ otherwise.

: send-arguments ( length -- SW1 true | false )
  ack-or-sw1? 0= if drop false exit then          \ No answer from card
  dup is-sw1? if nip true exit then               \ SW1 received
  =ins? if send-remaining exit then               \ Card wants all the bytes
  dup 0= if drop get-byte? exit then              \ Nothing left to send
  send-next-byte 1- dup if delay-bit then         \ Send next byte and go on
  recurse ;                                       \ Next step

: get-from-card ( max-length -- )
  sc-max-length !
  begin
    sc-max-length @ if get-byte? else false then while
    sc-addr!++ 1 sc-max-length -!
  repeat
;

: send-command ( -- ) i2c-data 2 + sc-addr ! 5 send-batch ;

: set-sc-len ( -- ) sc-addr @ i2c-data - i2c-buffer ! led-on ;
: set-sc-len-ok ( -- ) set-sc-len led-on ;
: set-sc-len-blink-fast ( -- ) set-sc-len led-blink-fast ;

: terminate-smartcard ( SW1 true | false -- )
  if sc-addr!++ get-byte? if sc-addr!++ set-sc-len-ok exit then then
  set-sc-len-blink-fast ;

: smartcard-command ( -- )
    led-blink
    \ Send command and arguments to the card, get SW1
    send-command
    \ If this is an incoming command for the card, send arguments
    i2c-data @ 5 - dup if
      delay-bit send-arguments terminate-smartcard exit
    then
    drop
    \ Prepare reception buffer
    i2c-buffer 1+ sc-addr !
    \ We may have incoming bytes to get from the card
    ack-or-sw1? 0= if led-blink-fast 0 i2c-buffer ! exit then
    \ If we have a SW1, read SW2; otherwise get the whole frame
    dup is-sw1? if true terminate-smartcard exit then
    drop i2c-data 1 + @ 2 + get-from-card set-sc-len-ok
;

: card-change? ( -- ) /off bit-set? sc-prev @ xor ;

: card-idle ( -- )
    i/o >input /cmdvcc high rstin low 0 sc-prev ! 0 atr-length ! ;

: handle-insertion ( -- )
    \ Debounce contact (do nothing if this is not a real card insertion)
    $20 sc-count v-for card-change? 0= if exit then v-next
    \ Record card presence
    led-blink /off bit-mask sc-prev !
    \ Use direct mode to read first ATR byte
    0 sc-inverse !
    \ Startup sequence (see TDA8004 datasheet)
    /cmdvcc low 140탎 rstin high
    \ Get first byte and check convention
    get-byte? 0= if rstin low led-blink-fast exit then
    dup $03 = if drop $3f 1 sc-inverse +! then atr !
    \ Receive rest of ATR
    atr 1+ sc-addr ! $f get-from-card sc-addr @ atr - atr-length !
    \ Smartcard is ready
    smartcard signal-event led-on
;

\ While power to the card is removed, it looks like some capacitors
\ do still present the card as being here. A 80ms delay takes care of
\ that situation.

: handle-removal ( -- ) led-off card-idle 80ms smartcard signal-event ;
    
: handle-smartcard ( -- )
    sc-prev @ if handle-removal exit then handle-insertion ;

\ ----------------------------------------------------------------------
\ I2C
\ ----------------------------------------------------------------------

\ As explained in Microchip MSSP Module Silicon Errata Sheet, it is sometimes
\ necessary to reset the I2C module after a timeout because of a hardware
\ bug with no simple workaround.

variable i2c-timeout

: reset-i2c-module ( -- ) sspen bit-clr sspen bit-set ;

:: write-i2c ( b -- ) sspbuf ! ckp bit-set ;

: read-i2c ( -- b )
  sspbuf @
  sspov bit-clr? if exit then
  \ Record overflow condition
  sspov bit-clr 1 i2c-overflow +! ;

macro
: ack-i2c ( -- ) sspif bit-clr ;
target

: i2c-bq-write ( -- ) i2c-data 1+ @ i2c-data @ bq! ;
: i2c-bq-read ( -- ) i2c-data @ bq@ i2c-buffer ! ;

: i2c-get-stats ( -- )
    i2c-overflow @ i2c-buffer !
    sc-parityerrors @ i2c-buffer 1+ ! sc-nack @ i2c-buffer 2 + !
    sc-tocard @ i2c-buffer 3 + ! sc-fromcard @ i2c-buffer 4 + !
;

: i2c-configure-pwm ( -- )
    i2c-data @ pr2 !
    i2c-data 1+ @ ccpr1l !
    i2c-data 2 + @ 2 rshift ccp1con @ $30 /and or ccp1con !
    i2c-data 3 + @ t2con @ 3 /and or t2con !
;

: i2c-start-pwm ( -- ) $f8 trisc ! tmr2on bit-set ;

: i2c-stop-pwm ( -- ) tmr2on bit-clr $fc trisc ! ;

: i2c-get-event ( -- ) event @ i2c-buffer ! 0 event ! ;

: i2c-change-devid ( -- ) i2c-data @ i2c-address ee! ;

: i2c-get-version ( -- ) vsn-major i2c-buffer c! vsn-minor i2c-buffer 1+ c! ;

: i2c-get-atr ( -- ) atr-length i2c-buffer atr-length @ 1+ cmove ;

\ There has been an incoming request, handle it
: i2c-incoming ( -- )
    \ Parameterless commands
    i2c-length @ 1 = if
      \ 0x01: get interface version information
      i2c-command @ $01 = if i2c-get-version exit then
      \ 0x04: get the stack pointer value
      i2c-command @ $04 = if fsr @ i2c-buffer ! exit then
      \ 0x06: call monitor (stop interrupts first)
      i2c-command @ $06 = if disable-interrupts ]asm 600 goto asm[ then
      \ 0x07: set interrupt line down
      i2c-command @ $07 = if interrupt low interrupt >output exit then
      \ 0x08: turn led on
      i2c-command @ $08 = if led-on exit then
      \ 0x09: turn led off
      i2c-command @ $09 = if led-off exit then
      \ 0x0a: make led blink
      i2c-command @ $0a = if led-blink exit then
      \ 0x0b: make led blink fast
      i2c-command @ $0b = if led-blink-fast exit then
      \ 0x20: get statistics
      i2c-command @ $20 = if i2c-get-stats exit then
      \ 0x21: reset statistics
      i2c-command @ $21 = if reset-stats exit then
      \ 0x22: get version string
      i2c-command @ $22 = if i2c-get-vsn exit then
      \ 0x23: get copyright
      i2c-command @ $23 = if i2c-get-copyright exit then
      \ 0x2a: get private data
      i2c-command @ $2a = if i2c-get-private exit then
      \ 0x31: start PWM
      i2c-command @ $31 = if i2c-start-pwm exit then
      \ 0x32: stop PWM
      i2c-command @ $32 = if i2c-stop-pwm exit then
      \ 0x40: get event
      i2c-command @ $40 = if i2c-get-event exit then
      \ 0x50: reset device
      i2c-command @ $50 = if reset then
      \ 0x60: get ATR (length + content)
      i2c-command @ $60 = if i2c-get-atr exit then
      \ 0x61: reset smartcard
      i2c-command @ $61 = if handle-removal exit then
      \ 0x70: do analog to digital conversion
      i2c-command @ $70 = if i2c-adc exit then
      \ 0x71: enter sleep mode
      i2c-command @ $71 = if sleep-mode exit then
    then
    \ Commands with one parameter
    i2c-length @ 2 = if
      \ 0x02: read one byte in bank 0 (addr)
      i2c-command @ $02 = if i2c-data @ @ i2c-buffer ! exit then
      \ 0x11: bq2010 read (addr)
      i2c-command @ $11 = if i2c-bq-read exit then
      \ 0x51: set i2c address
      i2c-command @ $51 = if i2c-change-devid exit then
    then
    \ Commands with two parameters
    i2c-length @ 3 = if
      \ 0x10: bq2010 write (addr, content)
      i2c-command @ $10 = if i2c-bq-write exit then
      \ 0x2b: private data write (addr, content)
      i2c-command @ $2b = if i2c-private-write then
    then
    \ Commands with four parameters
    i2c-length @ 5 = if
      \ 0x30: configure PWM
      i2c-command @ $30 = if i2c-configure-pwm exit then
    then
    \ Commands with variable number of parameters
    i2c-length @ 2 < if exit then
    i2c-command @ $62 = if
      \ 0x62: send command to smartcard
      i2c-length @ i2c-data @ - 3 = if smartcard-command exit then
    then
    \ Command is unknown or only partially available
;

: reset-i2c-buffer ( -- ) i2c-buffer i2c-addr ! ;

\ State 1: make a dummy read and reset reception area
: i2c-state1 ( -- )
    80 i2c-timeout !   \ Arm timer
    read-i2c drop 0 i2c-length ! reset-i2c-buffer ;

\ State 2: add byte to command
: i2c-state2 ( -- )
    40 i2c-timeout !   \ Arm timer
    read-i2c i2c-addr @ ! 1 i2c-addr +! 1 i2c-length +! i2c-incoming ;

\ State 4: send next byte
: i2c-state4 ( -- ) i2c-addr @ @ write-i2c 1 i2c-addr +! ;

\ State 3: reset reception area and send first byte
: i2c-state3 ( -- ) reset-i2c-buffer i2c-state4 ;

\ State 5: end of read, nothing to do
: i2c-state5 ( -- ) ;

: handle-i2c ( -- )
    \ Acknowledge i2c and reset timeout
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
    \ This is an inconsistent state, signal an error
    i2c-error signal-event ;

\ ----------------------------------------------------------------------
\ Interrupt handler
\ ----------------------------------------------------------------------

isr
: isr-handler ( -- )
    tmr1if bit-clr
    blinking-speed @ 1 and if
	1 blinking-counter -!
	z bit-set? if
	    led toggle blinking-speed @ blinking-counter !
	then
    then
    1 i2c-timeout -! z bit-set? if reset-i2c-module then
    1 watchdog-timeout -! z bit-set? if reset then
    isr-restore-return ;

\ ----------------------------------------------------------------------
\ Initialization and main program
\ ----------------------------------------------------------------------

: init ( -- )
    \ Timer0 is used with a prescaler of 8
    $02 option_reg !
    \ A/C converter on pin A0, with VREF+ and VREF-, left justified
    \ result, internal clock
    $8f adcon1 ! $c1 adcon0 !
    \ The LED (initially off) is an output
    led-off led >output
    \ Nothing happened yet but a startup
    startup event !
    \ PWM output is used as a host-controlled baud-rate generator
    ccp1con @ $0f /and $0c or ccp1con !   \ Activate PWM mode
    \ I2C slave mode is used
    $36 sspcon !          \ Enable I2C slave 7bit add., do not hold clock
    i2c-address ee@ 2* sspadd ! \ Set I2C address
    0 sspstat !           \ Clear all error condition bits
    0 sspcon2 !           \ Do not answer general call
    0 i2c-timeout !       \ Cf. variable declaration
    \ Ports b1, b2, c0 and c1 are outputs
    $f9 trisb !
    $fc trisc !
    clkdiv1 low clkdiv2 low   \ Divide frequency by 8
    card-idle                 \ By default, do as if card is not present
    \ Statistics counter
    reset-stats
    \ Setup watchdog
    clear-watchdog
    \ Timer 1
    $31 t1con ! tmr1ie bit-set peie bit-set enable-interrupts
;

main
: main ( -- )
    init startup signal-event
    begin
	clear-watchdog
	sspif bit-set? if handle-i2c then
	card-change? if handle-smartcard then
    again ;

\ Configuration word

fosc-hs set-fosc      \ High-speed (26MHz) oscillator
false set-wdte        \ Do not use watchdog timer
false set-lvp         \ No low-voltage programming
true set-boden        \ Brown-out reset
true set-wrt          \ Enable flash-writing (monitor)
false set-/pwrte      \ Power-up timer
