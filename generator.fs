\
\ Code for the DCC command station.
\
\ This program is in charge of:
\    * generating a valid DCC signal from commands given on the serial line
\    * do some reporting on the serial line
\
\ References:
\    * NMRA DCC Standards and Recommended Practices:
\      http://www.tttrains.com/nmradcc/

include picisr.fs
include piceeprom.fs
include libnibble.fs
include picflash.fs
include libstrings.fs

\ ---------------------------------------------------------------------- 
\ Options
\ ---------------------------------------------------------------------- 

variable option-byte
0 option-byte bit option-verbose
1 option-byte bit option-flow-control
2 option-byte bit option-idling
3 option-byte bit option-long-preamble

macro
: verbose? option-verbose bit-set? ;
: flow-control? option-flow-control bit-set? ;
: option-idling? option-idling bit-set? ;
: long-preamble? option-long-preamble bit-set? ;
target

\ ----------------------------------------------------------------------
\ Pinout used in this program
\ ----------------------------------------------------------------------

0 pin-a led-green               \ Green OK led (out)
1 pin-a led-red                 \ Red led (out)
2 pin-a lcd-d5                  \ LCD data (out)
3 pin-a lcd-d7                  \ LCD data (out)
4 pin-a lcd-d6                  \ LCD data (out)
5 pin-a lcd-d4                  \ LCD data (out)

0 pin-b bridge-dir              \ Bridge DIR (out)
1 pin-b bridge-brake            \ Bridge BRAKE (out)
2 pin-b bridge-pwm              \ Bridge PWM (out)
4 pin-b bridge-therm            \ Bridge THERM (in, active low)
5 pin-b bridge-sense1           \ Bridge current sense first comparator (in)
6 pin-b bridge-sense2           \ Bridge current sense second comparator (in)

1 pin-c lcd-enable              \ LCD enable (out)
3 pin-c lcd-rs                  \ LCD register select (out)
4 pin-c rts                     \ Serial port RTS (in)
5 pin-c cts                     \ Serial port CTS (out)

\ ----------------------------------------------------------------------
\ LCD control
\ ----------------------------------------------------------------------

\ Basic timing routines at 20MHz

variable timing1 variable timing2 variable timing3
: 80탎 $86 timing1 v-for v-next ;
: 1.6ms $e timing1 v-for $c8 timing2 v-for v-next v-next ;
: 30ms $c8 timing1 v-for $fa timing2 v-for v-next v-next ;

\ Send a pulse to indicate that data is ready

: lcd-pulse lcd-enable high nop nop nop lcd-enable low ;

\ Send a nibble

variable lcd-nibble

: lcd-send4lsb ( b -- )
  0 lcd-nibble !
  dup 8 and if $08 lcd-nibble or! then
  dup 4 and if $10 lcd-nibble or! then
  dup 2 and if $04 lcd-nibble or! then
  dup 1 and if $20 lcd-nibble or! then
  drop $3c porta /and! lcd-nibble @ porta or! lcd-pulse
;

variable lcd-last-col
: lcd-send8 ( b -- ) dup 4 rshift lcd-send4lsb nop nop lcd-send4lsb 80탎 ;
: lcd-emit ( b -- ) 1 lcd-last-col +! lcd-rs high lcd-send8 lcd-rs low ;
: lcd-print ( -- ) begin str-char dup while lcd-emit repeat drop ;
: lcd-clear ( -- ) 0 lcd-last-col ! $01 lcd-send8 1.6ms ;
macro
: lcd-put4 ( b -- ) nibble>hex lcd-emit ;
: lcd-space ( -- ) $20 lcd-emit ;
target
: lcd-put8 ( b -- ) dup 4 rshift lcd-put4 $f and lcd-put4 ;

\ Erase until end of line

: lcd-eol ( -- ) begin lcd-last-col @ $10 <> while $20 lcd-emit repeat ;

\ Go to first or second line

: lcd-first ( -- ) 0 lcd-last-col ! $80 lcd-send8 ;
: lcd-second ( -- ) 0 lcd-last-col ! $c0 lcd-send8 ;

macro
: lcd-erase-first ( -- ) lcd-first lcd-eol lcd-first ;
: lcd-erase-second ( -- ) lcd-second lcd-eol lcd-second ;
target

\ Set LCD in 4 bits mode, with blinking cursor

: lcd-setup ( -- )
  30ms
  $3 lcd-send4lsb 30ms $3 lcd-send4lsb 80탎 $3 lcd-send4lsb 80탎
  $2 lcd-send4lsb 80탎
  $28 lcd-send8 $08 lcd-send8 lcd-clear $06 lcd-send8 $0f lcd-send8
;

\ ----------------------------------------------------------------------
\ Advanced option manipulation
\ ----------------------------------------------------------------------

: +verbose option-verbose bit-set lcd-second c"   Manual  mode  " lcd-print ;
: -verbose option-verbose bit-clr lcd-second c"  Automatic mode " lcd-print ;

\ ----------------------------------------------------------------------
\ Serial port
\ ----------------------------------------------------------------------
\
\ The serial port uses RTS and CTS for flow control lines as described at
\ http://eintr.net/serial/pins_meanings.html. Flow control is optional.

:: emit ( b -- )
  >w
  begin txif bit-set? until
  flow-control? if begin rts high? until then
  w> txreg ! ;

: space ( -- ) $20 emit ;
: crlf ( -- ) $d emit $a emit ;
: print ( -- ) begin str-char dup while emit repeat drop ;

\ The following word could be written as `print crlf' but this would add
\ one more return-stack depth level.
: print-crlf ( -- ) begin str-char dup while emit repeat drop crlf ;

macro
: v-print ( -- ) verbose? if print then ;
: v-print-crlf ( -- ) verbose? if print-crlf then ;
: v-space ( -- ) verbose? if space then ;
target

macro
: clear-errors ( -- ) oerr bit-set? if cren bit-clr cren bit-set then ;
: key? ( -- f ) rcif bit-set? ;
target

: key ( -- b )
  flow-control? if cts high then
  begin key? until
  rcreg @ clear-errors
  flow-control? if cts low then
  verbose? 0= if exit then
  dup emit
;

: get8 ( -- b ) key hex>nibble 4 lshift key hex>nibble or ;
: put8 ( b -- ) dup 4 rshift nibble>hex emit $f and nibble>hex emit ;

\ ----------------------------------------------------------------------
\ DCC output control (electrical)
\ ----------------------------------------------------------------------

: enable-dcc-output ( -- ) bridge-brake low bridge-pwm high led-red low ;
: disable-dcc-output ( -- ) bridge-brake high bridge-pwm low led-red high ;

\ ----------------------------------------------------------------------
\ Interrupt driven DCC pulse generator
\ ----------------------------------------------------------------------

variable alerts
variable previous-alerts

1 constant alert-therm
2 constant alert-sense1

: therm-alert ( -- )
  alert-therm alerts or! disable-dcc-output ;

: sense1-alert ( -- )
  alert-sense1 alerts or! disable-dcc-output ;

\ set-timer will wait for 3.2*(256-n)-0.2 탎 for a 20MHz oscillator and a
\ 16 prescaler. For ones, use n=238 (57.4탎 => 1.03% error). For zero, use
\ values between n=0 (819.0탎) and n=224 (102.2탎).

macro
: set-timer ( n -- ) tmr0 +! ;
target

\ delay contains the duration of the next low pulse when the high pulse
\ is in progress or 0 otherwise

variable delay

\ pulse and half-pulse work the reverse way around: they set the timer
\ to the proper value, then call the idle-loop to do other tasks, then
\ wait for the timer to expire and immediately change the DCC output
\ state.  This way, it is permitted to take some time (up to 40탎,
\ i.e. around 200 instructions) to handle secondary tasks. More
\ complex tasks can be handled if the zero flag is true (in which case
\ we have up to 80탎, i.e. around 400 instructions).

macro
: half-pulse ( -- ) delay @ set-timer ;          \ Set timer to pulse value
target

:: pulse ( duration -- ) delay ! half-pulse clrwdt ;

: bit-zero ( -- ) $64 pulse ;
: bit-one ( -- )  $ee pulse ;

\ Data being transmitted. We implement a 16 bits shift register, left-aligned,
\ so that preamble, start and stop bits can be easily implemented. To write
\ something into the DCC generator, wait for data-bits to be zero, write
\ into data-high and data-low (left aligned) and set data-bits to the correct
\ number of bits.

variable data-high
variable data-low
variable data-bits

\ Internal data. Data is double buffered, to let the time to the main routine
\ to write the needed bytes. Idling takes place automatically if the main
\ routine fails to write a proper bit sequence into data-high, data-low and
\ data-bits.

variable dcc-high
variable dcc-low
variable dcc-bits

variable idling?

: idling-step ( -- )
  1 idling? +!
  idling? @ 1 = if $ff dcc-high ! $fc dcc-low ! $f dcc-bits ! exit then \ Pre
  idling? @ 2 = if $ff dcc-high ! 0 dcc-low ! 9 dcc-bits ! exit then \ All 1
  idling? @ 3 = if $0 dcc-high ! 0 dcc-low ! 9 dcc-bits ! exit then  \ All 0
  0 idling? ! $ff dcc-high ! $80 dcc-low ! 9 dcc-bits !              \ Checksum
;

\ Idle packets are automatically generated unless bit option-idling has been
\ set to zero. This is for debugging only, as this will generate
\ invalid DCC data as no more square waves will be generated.

: start-idling ( -- )
  option-idling? if
    0 idling? ! idling-step               \ Start idle sequence
  else
    1 dcc-bits !                          \ Transmit one bit
    0 delay !                             \ which will not toggle anything
  then
;

: dcc-refill ( -- )
  idling? @ if idling-step exit then
  data-bits @ 0= if start-idling exit then
  data-high @ dcc-high ! data-low @ dcc-low ! data-bits @ dcc-bits !
  0 data-bits !
;

\ isr-tmr0 is called when there is a timer overflow interrupt
\ This word is very long to avoid return stack depth exhaustion, but very
\ thoroughly commented. It terminates with a call to isr-exit to properly
\ exit from interrupt

isr : isr-tmr0 ( -- )
    t0if bit-clr                        \ Acknowledge interrupt
    bridge-therm low? if therm-alert then       \ Handle excessive temp
    \ XXXXX bridge-sense1 high? if sense1-alert then    \ Handle short-circuit
    delay @ if
	bridge-dir low                  \ Drive DCC output low
	half-pulse                      \ Rearm timer
	0 delay !                       \ Note low pulse in progress
    else
      bridge-dir high                     \ Drive DCC output high
      dcc-low rlf! dcc-high rlf!          \ Shift data left through carry
      c bit-set? if bit-one else bit-zero then   \ Send bit and clear watchdog
      1 dcc-bits -!                       \ Decrement data length
      z bit-set? if dcc-refill then       \ Refill transmit buffer
    then
    isr-restore-return
;

\ ----------------------------------------------------------------------
\ DCC encoder configuration
\ ----------------------------------------------------------------------

eevariable cfg-repeat
eevariable cfg-genidle
eevariable cfg-flow-control
eevariable cfg-verbose
eecreate cfg-magic 2 eeallot                  \ Magic number is $27 $05

\ Print the description of a variable as well as its address and current
\ value.

: explain-var ( n -- ) dup put8 space print [char] : emit space ee@ put8 crlf ;

: configuration-help ( -- )
  crlf
  cfg-repeat c" Number of times to repeat a command" explain-var
  cfg-genidle c" Generate idle packets (default: 1)" explain-var
  cfg-flow-control c" Use hardware flow control (default: 0)" explain-var
  cfg-verbose c" Verbose mode at reset (default: 1)" explain-var
;

: factory-defaults ( -- )
  3 cfg-repeat ee!
  1 cfg-genidle ee!
  0 cfg-flow-control ee!
  1 cfg-verbose ee!
  $27 cfg-magic ee!
  $05 cfg-magic 1+ ee!
;

\ Verbose is only set at startup and is not modified here

: read-configuration ( -- )
  cfg-genidle ee@ if option-idling bit-set else option-idling bit-clr then
  cfg-flow-control ee@ if
    option-flow-control bit-set
  else
    option-flow-control bit-clr
  then
  flow-control? 0= if cts high then   \ The other end may use flow control
;

\ If magic number is invalid, we revert to factory defaults. This prevents
\ EEPROM accidental erasing, some manipulation errors or addition of new
\ options in newer versions of this program.

: check-magic ( -- )
  cfg-magic ee@ $27 = if cfg-magic 1+ ee@ $05 = if exit then then
  crlf
  c" Bad configuration data, reverting to factory defaults" print-crlf
  factory-defaults
;

\ ----------------------------------------------------------------------
\ DCC data manipulation
\ ----------------------------------------------------------------------

variable checksum

\ Wait for the transmission slot to be available. The sequence has to be
\ as short as possible to avoid entering the idle mode between successive
\ bytes of the same command.

macro
: wait-for-slot ( -- ) begin data-bits @ 0= until ;
target

\ Transmit a preamble (14 bits to 1) and a start bit (one bit to 0)
\ A long preamble (20 bits to 1) may be transmitted if we are in service
\ mode.

: preamble ( -- )
  long-preamble? if wait-for-slot $fc data-high ! 6 data-bits ! then
  wait-for-slot 0 checksum ! $ff data-high ! $fc data-low ! $f data-bits !
;

\ send-byte sends a byte followed by a start bit. send-final-byte ends
\ the packet with a stop bit after the byte. send-checksum sends the checksum
\ with a stop bit.

: finish-byte ( b -- ) dup checksum xor! data-high ! 9 data-bits ! ;
:: send-byte ( b -- ) wait-for-slot $00 data-low ! finish-byte ;
: send-final-byte ( b -- ) wait-for-slot $80 data-low or! finish-byte ;
: send-checksum ( -- ) checksum @ send-final-byte ;

\ Send a packet located at cmd with length in cmd-length

variable cmd-length
create cmd 5 allot

variable cmd-tmp
: dcc-packet ( -- )
  preamble cmd-length @ cmd-tmp !
  cmd @ send-byte 1 cmd-tmp -! z bit-set? if send-checksum exit then
  cmd 1+ @ send-byte 1 cmd-tmp -! z bit-set? if send-checksum exit then
  cmd 2 + @ send-byte 1 cmd-tmp -! z bit-set? if send-checksum exit then
  cmd 3 + @ send-byte 1 cmd-tmp -! z bit-set? if send-checksum exit then
  cmd 4 + @ send-byte send-checksum
;

variable repeat-tmp
:: dcc-packet-n ( n -- ) repeat-tmp v-for dcc-packet v-next ;
: dcc-packet-repeat ( -- ) cfg-repeat ee@ dcc-packet-n ;

\ General reset command

: dcc-on-off ( -- ) $a repeat-tmp v-for 30ms v-next ;
: dcc-reset-packet ( -- ) 2 cmd-length ! 0 cmd ! 0 cmd 1+ ! ;
: dcc-reset ( -- ) dcc-reset-packet dcc-packet-repeat ;
: dcc-enter-service-mode ( -- )
  option-long-preamble bit-set dcc-on-off dcc-reset-packet 3 dcc-packet-n ;
: dcc-leave-service-mode ( -- )
  dcc-reset-packet 6 dcc-packet-n option-long-preamble bit-clr dcc-on-off ;
: dcc-paged ( v a -- ) 2 cmd-length ! cmd 1+ ! $78 or cmd ! 5 dcc-packet-n ;

\ ----------------------------------------------------------------------
\ Commands handling
\ ----------------------------------------------------------------------

: ack ( -- ) v-space [char] ! emit v-space ;

: get8/w ( -- b/w ) v-space get8 >w ;
macro
: get8w get8/w w> ;
target

: get-dcc-packet ( -- )
  get8 dup cmd-length ! cmd-tmp !
  get8w cmd ! 1 cmd-tmp -! z bit-set? if exit then
  get8w cmd 1+ ! 1 cmd-tmp -! z bit-set? if exit then
  get8w cmd 2 + ! 1 cmd-tmp -! z bit-set? if exit then
  get8w cmd 3 + ! 1 cmd-tmp -! z bit-set? if exit then
  get8w cmd 4 + !
;

: cmd-any-packet ( -- )
  c" ny packet " v-print
  get-dcc-packet
  ack dcc-packet-repeat
;

\ Programming using direct access
: cmd-direct-access ( -- )
  c" rogramming (direct) packet " v-print
  \ Get a 10 (16) bits address and a 8 bits value on the stack
  get8 get8 v-space get8 ack
  \ Send 5 service-mode packets
  dcc-enter-service-mode
  3 cmd-length ! cmd 2 + ! cmd 1+ ! $7c or cmd ! 5 dcc-packet-n
  dcc-leave-service-mode
;

\ Programming using paged access
: cmd-paged-access ( -- )
  c" rogramming (paged) packet " v-print
  \ Get a register address and a value on the stack
  get8 v-space get8 ack
  \ Preset page
  dcc-enter-service-mode 1 5 dcc-paged dcc-leave-service-mode
  \ Send data from values on stack
  dcc-enter-service-mode dcc-paged dcc-leave-service-mode
;

: cmd-emergency-stop ( -- )
  c" top engine immediately " v-print
  2 cmd-length !
  lcd-erase-first c"  Stop engine:" lcd-print
  get8 dup lcd-put8 cmd ! $41 cmd 1+ ! dcc-packet-repeat ack lcd-space
;

: >speed-n ( b -- s ) 1- c bit-clr rrf-tos c bit-set? if $10 or then $42 + ;
: >speed ( b -- s ) dup if >speed-n exit then drop $40 ;

: cmd-fwd ( -- )
  c" orward " v-print
  2 cmd-length !
  lcd-erase-first c" Eng:" lcd-print
  get8 dup lcd-put8 cmd !
  c"  Speed:+" lcd-print
  v-space get8 dup lcd-put8 >speed $20 or cmd 1+ !
  ack dcc-packet-repeat
;

: cmd-bwd ( -- )
  c" ackward " v-print
  2 cmd-length !
  lcd-erase-first c" Eng:" lcd-print
  get8 dup lcd-put8 cmd !
  c"  Speed:-" lcd-print
  v-space get8 dup lcd-put8 >speed cmd 1+ !
  ack dcc-packet-repeat
;

: cmd-setcfg ( -- )
  c" onfiguration set " v-print
  get8 v-space get8 ack swap ee!
  read-configuration ;

: cmd-getcfg ( -- )
  c" onfiguration get " v-print
  get8 ack ee@ put8 ;

: cmd-reset ( -- )
  dcc-reset
  lcd-first c" Immediate Reset!" lcd-print
  c" eset immediately" v-print
  ack ;

: cmd-verbon ( -- ) c" verbose mode on" print +verbose ack ;

: cmd-verboff ( -- ) -verbose ack ;

: cmd-config-help ( -- )
  c" elp on configuration" v-print
  ack configuration-help ;

: version c" DCC PIC 0.332" ;

: cmd-factory-reset ( -- )
  c" actory defaults" v-print
  ack factory-defaults ;

: cmd-dcc-disable ( -- )
  disable-dcc-output
  c" isable DCC output" v-print
  ack ;

: cmd-dcc-enable ( -- )
  c" nable DCC output" v-print
  ack enable-dcc-output ;

: cmd-help ( -- )
  ack crlf version print-crlf
  c" f loco speed            Move loco in forward direction" print-crlf
  c" b loco speed            Move loco in backward direction" print-crlf
  c" s loco                  Emergency stop" print-crlf
  c" r                       Emergency reset" print-crlf
  c" c nn                    Get configuration parameter" print-crlf
  c" C nn vv                 Set configuration parameter" print-crlf
  c" v                       Turn verbose mode on" print-crlf
  c" V                       Turn verbose mode off" print-crlf
  c" a length xx yy zz ...   Send any valid DCC packet" print-crlf
  c" h                       List of configuration parameters" print-crlf
  c" F                       Factory defaults" print-crlf
  c" d                       Disable DCC output" print-crlf
  c" E                       Enable DCC output" print-crlf
  c" P                       Program register (direct access)" print-crlf
  c" p                       Program register (paged access)" print-crlf
  c" ?                       Help (this message)" print-crlf
  c" X                       Reset DCC encoder" print
;

: nack ( c -- c )
nop nop nop nop
nop nop nop nop
nop nop nop nop
nop nop nop nop
nop nop nop nop
  verbose? if
    crlf c" ??? Unknown-command: `" print dup emit [char] ' emit exit
  then
  [char] ? emit
;

: handle-command ( c -- c )
  dup [char] f = if cmd-fwd exit then
  dup [char] b = if cmd-bwd exit then
  dup [char] s = if cmd-emergency-stop exit then
  dup [char] r = if cmd-reset exit then
  dup [char] c = if cmd-getcfg exit then
  dup [char] C = if cmd-setcfg exit then
  dup [char] v = if cmd-verbon exit then
  dup [char] V = if cmd-verboff exit then
  dup [char] a = if cmd-any-packet exit then
  dup [char] h = if cmd-config-help exit then
  dup [char] F = if cmd-factory-reset exit then
  dup [char] d = if cmd-dcc-disable exit then
  dup [char] E = if cmd-dcc-enable exit then
  dup [char] P = if cmd-direct-access exit then
  dup [char] p = if cmd-paged-access exit then
  dup [char] ? = if cmd-help exit then
  dup [char] X = if led-green low reset then
  dup 32 < if exit then
  nack
;

\ ----------------------------------------------------------------------
\ Alert handling and notification
\ ----------------------------------------------------------------------

\ If an alert is cleared and re-raised immediately, it will go through
\ the same handling procedures some time later. Hopefully, this should
\ not arrive. Otherwise, time are short enough not to damage any
\ component.

: alerts-store ( alerts -- )
  dup 0= if led-green high then                  \ No more alerts
  dup previous-alerts ! alerts !
;

: alert-set-therm ( alerts -- )
  previous-alerts @ alert-therm and 0= if         \ Alert was not known before
    c" *** THERMAL ALERT ***" v-print-crlf
    led-green low
  then
  bridge-therm high? if
    alert-therm /and
    c" *** END OF THERMAL ALERT ***" v-print-crlf
  then
  alerts-store
;

: alert-set-sense1 ( alerts -- )
  previous-alerts @ alert-sense1 and 0= if        \ Alert was not known before
    c" *** SHORT CIRCUIT ALERT ***" v-print-crlf
    led-green low
  then
  bridge-sense1 low? if
    alert-sense1 /and
    c" *** END OF SHORT CIRCUIT ALERT ***" v-print-crlf
  then
  alerts-store
;

\ A thermal alert is handled first, as it is the most critical

: alert-set ( -- )
  alerts @
  dup alert-therm and if alert-set-therm exit then
  dup alert-sense1 and if alert-set-sense1 exit then
  alerts-store
;

\ ----------------------------------------------------------------------
\ Initialization
\ ----------------------------------------------------------------------

: init ( -- )
    \ Data
    $ff dcc-high  !              \ 16 bits to one will help the system
    $ff dcc-low !                \ to enter permanent mode
    $10 dcc-bits !
    \ Port A
    $06 adcon1 !                 \ Disable A/D converter
    $00 porta !                  \ Clear output latches
    $c0 trisa !                  \ Port 0 to 5 as outputs
    \ Port B
    $00 portb !                  \ Clear output latches
    $f8 trisb !                  \ Ports 0, 1 and 2 as outputs
    \ Port C
    $00 portc !                  \ Clear output latches
    $95 trisc !                  \ Ports 1, 3, 5 and 6 (TX) as outputs
    \ Serial port
    $14 spbrg !                  \ 57600 bauds with BGRH high at 20MhZ
    \ $19 spbrg !                  \ 9600 bauds with BGRH high at 4MhZ
    $24 txsta !                  \ Asynchronous transmit high-speed generator
    $90 rcsta !                  \ Serial-enable 8bits-rx continuous-receive
    \ Timer 0 for DCC generation
    $83 option_reg !   \ CLKOUT, prescaler to timer 0, prescaler=16, no pullups
    t0ie bit-set                 \ Enable timer 0 overflow interrupt
    $f0 tmr0 !                   \ Do not wait too long before first interrupt
    \ Alerts
    0 alerts !                   \ No alert at boot time
    0 previous-alerts !          \ No previous alerts
    \ Start interrupts before LCD, to enable watchdog clearing
    enable-interrupts
    \ LCD
    lcd-setup                    \ Set LCD in 4 bits mode
;

: greetings ( -- )
  lcd-first version lcd-print lcd-eol
  version v-print-crlf ;

\ ----------------------------------------------------------------------
\ Main program and main loop
\ ----------------------------------------------------------------------

: prompt ( -- )
  verbose? if crlf c" Command (type `?' for help) > " print exit then
  [char] > emit ;

\ Wait for a key and do some non-urgent things in the meantime

: wait-for-key ( -- )
  flow-control? if cts high then
  begin
    key? 0= while
    alerts @ if alert-set then
  repeat ;

: mainloop ( -- )
  begin
    prompt wait-for-key key handle-command drop
    \ Use the following line for debugging purpose
    \ lcd-second c" Stack: " lcd-print 4 @ lcd-put8 lcd-eol
  again ;

main
: main ( -- )
  init led-green high check-magic
  cfg-verbose ee@ if +verbose else -verbose then
  read-configuration
  enable-dcc-output dcc-reset greetings mainloop ;

\ ----------------------------------------------------------------------
\ Configuration word
\ ----------------------------------------------------------------------

fosc-hs set-fosc    \ High-speed oscillator
false set-/pwrte    \ Power-up reset timer
false set-lvp       \ No need for low voltage programming
