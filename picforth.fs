\
\ PicForth: Forth compiler for Microchip PIC 16F87x/16F88
\
\ Copyright (c) 2002-2004 Samuel Tardieu <sam@rfc1149.net>
\
\ This compiler is released under the GNU General Public License version 2,
\ see the COPYING file in the same directory, as well as the README.
\

hex

\ ----------------------------------------------------------------------
\ PicForth name spaces
\ ----------------------------------------------------------------------

\ We do use several vocabularies in PicForth:
\   + picassembler: contains the postfix/prefix PIC assembler
\   + picforth:     contains the code compiled for the PIC
\   + metacompiler: contains code needed for meta compilation, such as
\                   immediate words

vocabulary picassembler
vocabulary picforth
vocabulary metacompiler
vocabulary macrowords

\ We do use several modes here:
\   + picasm
\       In this mode, definitions go the picassembler vocabulary. forth words
\       are visible, as well as picassembler ones and then metacompiler ones.
\   + meta
\       In this mode, definitions go to the metacompiler vocabulary. forth
\       words are visible, as well as metacompiler ones. picassembler words
\       are next, so that immediate words can be defined using the assembler,
\       then the picforth word list to ease reference to already defined
\       words. Care must be taken in this mode as newly defined words may be
\       hidden by existing forth words. In short, meta should only be used
\       to define words that can be seen when compiling for the target.
\   + target
\       In this mode, we do compile for the target. Target words are seen
\       first, then metacompiler ones. Note that forth words are not visible
\       to prevent mistakes (such as using a word that has not been defined
\       in picforth but is present in the host's vocabulary).
\   + host
\       Regular mode: forth dictionary is first and inserted into, then
\       the picforth and picassembler dictionary.
\   + macrowords
\       Words : and ; come from the host vocabulary, others from the
\       metacompiler or target ones.

: picasm only metacompiler also picassembler definitions also forth ;
: meta only picforth also picassembler also metacompiler definitions
    also forth ;
: target only metacompiler also picforth definitions ;
: host only picassembler also picforth also forth definitions ;
: macro only picforth also metacompiler definitions also macrowords ;

\ Import a word from the forth dictionary into the metacompiler wordlist
\ (has to be executed in meta mode)

: import: ( "name" -- )
    >in @ bl parse sfind drop swap >in ! create , does> @ execute ;
    
\ ----------------------------------------------------------------------
\ Memory allocation
\ ----------------------------------------------------------------------

host

variable current-mode
variable current-area

: data-mode: create 0 , does> current-mode ! ;

data-mode: udata
data-mode: idata

meta

import: udata
import: idata

\ Structure of a section:
\   - current index (address in target memory)
\   - low address in target memory
\   - high address in target memory
\   - data mode
\   - previous section in same mode
\   - data itself

host

: 'data-org current-area @ ;
: data-here 'data-org @ ;
: data-low current-area @ 1 cells + @ ;
: data-high current-area @ 2 cells + @ ;
: data-mode current-area @ 3 cells + @ ;
: data-previous current-area @ 4 cells + @ ;
: data-base-addr current-area @ 5 cells + ;

: data-bounds data-low data-high 1+ ;
: data-size data-bounds swap - ;

: data-change-high current-area @ 2 cells + ! ;

: data-org ( addr -- ) 'data-org ! ;

: data-check ( addr -- ) data-bounds within 0= abort" Out of bank space" ;
: t>abs data-low - cells data-base-addr + ;
: t! dup data-check t>abs ! ;
: t@ t>abs @ ;

\ Uninitialized data is marked with 10000 (this is doable because host
\ cells are bigger than target cell).

: initialized? t@ f0000 and  10000 <> ;

meta

: section ( low high "name" -- )
    swap 2dup
    create here >r dup , , , current-mode @ dup , @ , - 1+ 0 do 10000 , loop 
    r> current-mode @ !
does> current-area ! data-mode current-mode ! ;

\ Some devices do only have the first two banks. It is the
\ programmer's responsability not to use non-existent banks.

idata
020 07f section bank0
0a0 0ef section bank1
120 16f section bank2
1a0 1ef section bank3

\ Default is to work in bank 0

bank0

host

\ We are not being strict here. At this time, we assume that all the
\ code fits in 2kwords (11 bits addressing) and that main data fits in one
\ bank (except special registers).

2000 constant code-size

\ This bit-set tells whether this code cell has been used or not
code-size allocate throw constant tused
: used tused + true swap c! ;
: used? tused + c@ ;
: init-tused tused code-size bounds do false i c! loop ;
init-tused

\ tcode holds the code
code-size cells allocate throw constant tcode
0 value tcshere
: tcs! dup used cells tcode + ! ;
: tcs@ cells tcode + @ ;

\ This space contains annotations explaining what the code does. For
\ example, code to normalize booleans gets annotated so that it can
\ be removed by test branches.

code-size cells allocate throw constant tnotes
: tnotes! cells tnotes + ! ;
: tnotes@ cells tnotes + @ ;

\ This space contains copies of literals or target addresses with all
\ significant bits set

code-size cells allocate throw constant tlit
: tcslit! cells tlit + ! ;
: tcslit@ cells tlit + @ ;

\ This space contains non-zero if code is actually data
code-size allocate throw constant tcsdata
: tcsdata@ tcsdata + c@ ;
: tcsdata! tcsdata + c! ;
: init-codedata tcsdata code-size bounds do 0 i c! loop ;
init-codedata

\ EEPROM

100 cells allocate throw constant teeprom

0 value teehere
: tee! cells teeprom + >r $ff and r> ! ;
: tee@ cells teeprom + @ ;
: tee-used? cells teeprom + @ -1 <> ;
: init-eeprom teeprom 100 cells bounds do -1 i ! 1 cells +loop ;
init-eeprom

\ Configuration words 

variable configword-1
3fff configword-1 !

: config-mask-1 invert configword-1 @ and or configword-1 ! ;

\ Aliases for configword-1 and config-mask-1
: configword configword-1 ;
: config-mask config-mask-1 ;

variable configword-2
3fff configword-2 !

: config-mask-2 invert configword-2 @ and or configword-2 ! ;

meta

: set-fosc 1f config-mask-1 ;
00 constant fosc-lp
01 constant fosc-xt
02 constant fosc-hs

: config-switch-1
    create ,
does>
    @ swap if dup else 0 swap then config-mask-1
;

004 config-switch-1 set-wdte
008 config-switch-1 set-/pwrte
020 config-switch-1 set-mclre
040 config-switch-1 set-boden
080 config-switch-1 set-lvp
100 config-switch-1 set-cpd
200 config-switch-1 set-wrt
800 config-switch-1 set-debug

\ It looks like boden/boren may be interchanged

: set-boren set-boden ;

: set-cp 3030 config-mask-1 ;
3030 constant no-cp
0000 constant full-cp

: set-ccp1 1000 config-mask-1 ;
0000 constant ccp1-rb3
1000 constant ccp1-rb0

: set-wrt 600 config-mask-1 ;
200 constant page1-wrt
400 constant page0-wrt
600 constant no-wrt

: config-switch-2
    create ,
does>
    @ swap if dup else 0 swap then config-mask-2
;

\ ----------------------------------------------------------------------
\ Annotations
\ ----------------------------------------------------------------------

\ Annotations are used along with code to remember whether some code
\ has been generated specifically to test for carry or zero status bits.
\ In this case, if a test of the corresponding bit occurs just after this
\ code, it will be removed and replaced by a direct bit test or conditional
\ jump instruction if possible. This allows us to generate good code even
\ since we are using a one-pass model.

host

1 constant note-z
2 constant note-c
4 constant note-invert
8 constant note-first
note-z note-invert or constant note-/z
note-c note-invert or constant note-/c

variable note

: >note note-first or note ! ;
: no-first [ note-first invert ] literal note @ and note ! ;
: no-note 0 >note ;

\ ----------------------------------------------------------------------
\ Compiler logic
\ ----------------------------------------------------------------------

host

: set true swap ! ;
: unset false swap ! ;

\ deadcode? indicates whether the current code point is reachable (false)
\ or not (true)

variable deadcode?

\ optimization control
create opt-allowed 1 ,
: opt-allowed? opt-allowed @ ;

meta

: allow-optimizations 1 opt-allowed ! ;
: disallow-optimizations 0 opt-allowed ! ;

host

\ code-depth represents the number of code words which constitute a single
\ block: no jump starts from within this block and no jump can arrive in
\ the middle of this block.

variable code-depth

: add-depth code-depth +! ;
: inc-depth 1 add-depth ;
: dec-depth -1 add-depth ;
: check-code-area tcshere code-size >= abort" out of code space" ;
: org to tcshere check-code-area ;
: (cs,) tcshere tcs! note @ tcshere tnotes! tcshere 1+ org inc-depth no-first ;
: cs, deadcode? @ if drop exit then (cs,) ;
: csdata, 1 tcshere tcsdata! (cs,) ;
: l-cs, tcshere tcslit! cs, ;
: prevlastcs tcshere 2 - tcs@ ;
: prevprevlastcs tcshere 3 - tcs@ ;
: lastcs tcshere 1- tcs@ ;
: kill-note 0 tcshere tnotes! ;
: cs-rewind tcshere 1- org kill-note dec-depth ;
: cs-rewind2 cs-rewind cs-rewind ;
: cs-rewind3 cs-rewind2 cs-rewind ;
: cs-unwind lastcs cs-rewind ;
: l-cs-unwind lastcs tcshere 1- tcslit@ cs-rewind ;
: cs-unwind2 cs-unwind cs-unwind ;
: cs-unwind3 cs-unwind2 cs-unwind ;
: no-opt 0 code-depth ! ;
: opt? opt-allowed? 0 code-depth @ < and ;
: opt2? 1 code-depth @ < ;
: opt3? 2 code-depth @ < ;

\ tcompile is the equivalent of state for the cross-compiler

variable tcompile
: +tcompile tcompile set ;
: -tcompile tcompile unset ;
: tcompile? tcompile @ ;

\ Incorporate immediate words from forth so that we can use them
\ while in target mode

meta

: ( postpone ( ;
: \ postpone \ ;
import: meta
import: host
import: macro
import: target
import: org
: +org tcshere + org ;

\ Include common base change commands

: decimal decimal ;
: hex hex ;

host

\ The forth> word eases debugging. You can write forth> expression ('til
\ the end of line), and it will get evaluated with the regular forth
\ dictionary placed first. The meta> word does the same thing with the
\ metacompiler vocabulary and is immediate as it is used in other
\ definitions. The target> word does the same thing with the target
\ vocabulary first.

: evaluate-eol source >in @ /string evaluate postpone \ ;
: forth> also forth evaluate-eol previous ;
: meta> also metacompiler evaluate-eol previous ; immediate
: target> also picforth evaluate-eol previous ; immediate

meta

import: forth>

\ ----------------------------------------------------------------------
\ Prefix assembler, with postfix mode
\ ----------------------------------------------------------------------

picasm

\ Allow the assembler to use a prefix notation if the user wants it instead
\ of a postfix one

variable prefix?
: prefix prefix? set ;
: postfix prefix? unset ;
: >prefix prefix? @ if >r evaluate-eol r> then ;

: ] +tcompile also picforth ;
: [ -tcompile previous ;

\ Parameterless opcodes
: clrw   103 cs, ;
: clrwdt 064 cs, ;
: nop    000 cs, ;
: retfie 009 cs, ;
: return 008 cs, ;
: sleep  063 cs, ;

\ Byte oriented file register operations
0 value f?
: ,f [ 1 7 lshift ] literal to f? ;
: ,w 0 to f? ;
: bofro: create 8 lshift , does> @ >prefix swap 7f and or f? or cs, ;
7 bofro: addwf
5 bofro: andwf
9 bofro: comf
3 bofro: decf
b bofro: decfsz
a bofro: incf
f bofro: incfsz
4 bofro: iorwf
8 bofro: movf
d bofro: rlf
c bofro: rrf
2 bofro: subwf
e bofro: swapf
6 bofro: xorwf
: clrf  >prefix 7f and 180 or cs, ;
: movwf >prefix 7f and 080 or cs, ;

\ Bit oriented file register operations
: bfro: create 4 or A lshift ,
        does> @ >prefix swap 7 lshift or swap 7f and or cs, ;
0 bfro: bcf
1 bfro: bsf
2 bfro: btfsc
3 bfro: btfss

\ Literal operations
: lo: create 8 lshift , does> @ >prefix swap ff and or cs, ;
3e lo: addlw
39 lo: andlw
38 lo: iorlw
30 lo: movlw
34 lo: retlw
3c lo: sublw
3a lo: xorlw

\ Control operations
: co: create 8 lshift , does> @ >prefix swap 7ff and or cs, ;
20 co: call
28 co: goto

\ Since call is also a gforth word, hide it with the picasm one
forth-wordlist set-current also picassembler : call call ; definitions previous

\ ----------------------------------------------------------------------
\ Literals operations
\ ----------------------------------------------------------------------

\ Do we have an operation working on the top of stack?
: opf? opt2? if lastcs 0800 = prevlastcs 30ff and 80 = and exit then false ;

\ Do we have a push?
: pushw? opt2? if lastcs 0080 = prevlastcs 0384 = and exit then false ;

\ Do we have a pop?
: pop? opt? if lastcs 0a84 = exit then false ;

: pop
    \ pushw and pop cancel
    pushw? if cs-rewind2 exit then
    opf? if
	\ Operation on top of stack can be made on w
	cs-rewind cs-unwind 80 xor
	\ Use 0103 for clrw instead of 0100 (problem on some hardware)
	dup 0100 = if 3 or then
	\ If we have a push before, cancel it as well
	opt? if lastcs 0384 = if cs-rewind cs, exit then then
	\ Use modified operation otherwise
	cs,
    then
    \ Increment stack pointer
    4 ,f incf ;

\ Is W already equal to top-of-stack?
: w=tos?
    opt? if lastcs 0800 = lastcs 0080 = or if true exit then then
    \ If we have a file operation with target in a file, W had been loaded
    \ previously and the file is neither 0 or 4, then W is equal to TOS
    opt2? if
	\ File operation
	lastcs 3000 and 0=
	\ Operating on a file
	lastcs 80 and 80 = and
	\ File is not indf
	lastcs 7f and 0 <> and
	\ File is not fsr
	lastcs 7f and 4 <> and
	\ Previous operation was a load or save
	prevlastcs 0800 = prevlastcs 0080 = or and
	exit
    then
    false ;

: loadw w=tos? if exit then 0 ,w movf ;
: storew w=tos? if exit then 0 movwf ;
: push pop? if cs-rewind exit then 4 ,f decf ;
: pushw push 0 movwf ;
: popw loadw pop ;

meta

\ Import those in the meta vocabulary

import: push     import: pop       import: loadw
import: storew   import: pushw     import: popw
import: nop

: >w popw ;
: w> pushw ;

host

: literal? ( -- n ) tcshere tcslit@ ;
: literal! ( n -- ) tcshere tcslit! ;
: load-literalw ( n -- ) dup literal! ff and movlw ;

: (literal)
    dup ff and if
	pop? if cs-rewind load-literalw storew else load-literalw pushw then
    else
	literal! meta> push
	0 clrf
    then ;

meta

: literal (literal) ;

\ ----------------------------------------------------------------------
\ Macro words    
\ ----------------------------------------------------------------------

host

: macro? s" macro!" evaluate ;
: macro! ;

only forth also macrowords definitions also forth
: macro-] state on ;
: macro-[ state off ; immediate
: ] macro-] ;
: [ postpone macro-[ ; immediate
: macro-(:noname) docol: cfa, defstart macro-] :-hook ;
: : header macro-(:noname) ;
: ;  ;-hook ?struc postpone exit reveal postpone macro-[ ; immediate
: macro! postpone (literal) ;
: ( postpone ( ; immediate
: ) ) ;
: \ postpone \ ; immediate

\ ----------------------------------------------------------------------
\ Constants
\ ----------------------------------------------------------------------

host

: (t-constant)
    @ tcompile? if
	(literal)
    else
	state @ if
	    postpone literal macro?
	then
    then ;

meta

\ Constants in PicForth are immediate words which do the right thing
\ depending on the current target compilation mode.

: constant ( n "name" -- ) create , immediate does> (t-constant) ;

: [ -tcompile also forth ;
: ] +tcompile previous ;

: 2constant ( u "name" -- ) ( run: -- high low )
    create dup 8 rshift , $ff and , immediate does>
    >r r@ (t-constant) r> cell+ (t-constant) ;

: bit ( n port "name" -- )
    100 * + meta> 2constant
;

target

\ Common constants

-1 constant true
0 constant false

  00 constant  indf
  01 constant  tmr0
  02 constant  pcl
  03 constant  status
  04 constant  fsr
  05 constant  porta
  06 constant  portb
  07 constant  portc
  08 constant  portd
  09 constant  porte
  0a constant  pclath
  0b constant  intcon
  0c constant  pir1
  0d constant  pir2
  0e constant  tmr1l
  0f constant  tmr1h
  10 constant  t1con
  11 constant  tmr2
  12 constant  t2con
  13 constant  sspbuf
  14 constant  sspcon
  15 constant  ccpr1l
  16 constant  ccpr1h
  17 constant  ccp1con
  18 constant  rcsta
  19 constant  txreg
  1a constant  rcreg
  1b constant  ccpr2l
  1c constant  ccpr2h
  1d constant  ccp2con
  1e constant  adresh
  1f constant  adcon0

  81 constant  option_reg
  85 constant  trisa
  86 constant  trisb
  87 constant  trisc
  88 constant  trisd
  89 constant  trise
  8c constant  pie1
  8d constant  pie2
  8e constant  pcon
  8f constant  osccon
  90 constant  osctune
  91 constant  sspcon2
  92 constant  pr2
  93 constant  sspadd
  94 constant  sspstat
  98 constant  txsta
  99 constant  spbrg
  9b constant  ansel
  9c constant  cmcon
  9d constant  cvrcon
  9e constant  adresl
  9f constant  adcon1

 105 constant  wdtcon
 10c constant  eedata
 10d constant  eeadr
 10e constant  eedath
 10f constant  eeadrh

 18c constant  eecon1
 18d constant  eecon2

( status bits )

  7 status bit irp
  6 status bit reg-rp1
  5 status bit reg-rp0
  4 status bit not_to
  3 status bit not_pd
  2 status bit z
  1 status bit dc
  0 status bit c

( intcon bits )

  7 intcon bit gie
  6 intcon bit peie
  5 intcon bit t0ie
  4 intcon bit inte
  3 intcon bit rbie
  2 intcon bit t0if
  1 intcon bit intf
  0 intcon bit rbif

( option_reg bits )

  7 option_reg bit /rbpu
  6 option_reg bit intedg
  5 option_reg bit t0cs
  4 option_reg bit t0se
  3 option_reg bit psa
  2 option_reg bit ps2
  1 option_reg bit ps1
  0 option_reg bit ps0

( eecon1 bits )

  7 eecon1 bit eepgd
  3 eecon1 bit wrerr
  2 eecon1 bit wren
  1 eecon1 bit wr
  0 eecon1 bit rd

( txsta bits )

  7 txsta bit csrc
  6 txsta bit tx9
  5 txsta bit txen
  4 txsta bit sync
  2 txsta bit brgh
  1 txsta bit trmt
  0 txsta bit tx9d

( rcsta bits )

  7 rcsta bit spen
  6 rcsta bit rx9
  5 rcsta bit sren
  4 rcsta bit cren
  3 rcsta bit adden
  2 rcsta bit ferr
  1 rcsta bit oerr
  0 rcsta bit rx9d

( pir1 bits )

  7 pir1 bit pspif
  6 pir1 bit adif
  5 pir1 bit rcif
  4 pir1 bit txif
  3 pir1 bit sspif
  2 pir1 bit ccp1if
  1 pir1 bit tmr2if
  0 pir1 bit tmr1if

( pir2 bits )

  7 pir2 bit osfif
  6 pir2 bit cmif
  4 pir2 bit eeif

( pie1 bits )

  7 pie1 bit pspie
  6 pie1 bit adie
  5 pie1 bit rcie
  4 pie1 bit txie
  3 pie1 bit sspie
  2 pie1 bit ccp1ie
  1 pie1 bit tmr2ie
  0 pie1 bit tmr1ie
  
( pie2 bits )

  7 pie2 bit osfie
  6 pie2 bit cmie
  4 pie2 bit eeie

( t1con bits )

  6 t1con bit t1run
  5 t1con bit t1ckps1
  4 t1con bit t1ckps0
  3 t1con bit t1oscen
  2 t1con bit /t1sync
  1 t1con bit tmr1cs
  0 t1con bit tmr1on

( t2con bits )

  6 t2con bit toutps3
  5 t2con bit toutps2
  4 t2con bit toutps1
  3 t2con bit toutps0
  2 t2con bit tmr2on
  1 t2con bit t2ckps1
  0 t2con bit t2ckps0
  
( sspcon bits )

  7 sspcon bit wcol
  6 sspcon bit sspov
  5 sspcon bit sspen
  4 sspcon bit ckp
  3 sspcon bit sspm3
  2 sspcon bit sspm2
  1 sspcon bit sspm1
  0 sspcon bit sspm0

( sspcon2 bits )

  7 sspcon2 bit gcen
  6 sspcon2 bit ackstat
  5 sspcon2 bit ackdt
  4 sspcon2 bit acken
  3 sspcon2 bit rcen
  2 sspcon2 bit pen
  1 sspcon2 bit rsen
  0 sspcon2 bit sen

( sspstat bits )

  7 sspstat bit smp
  6 sspstat bit cke
  5 sspstat bit d/a
  4 sspstat bit p
  3 sspstat bit s
  2 sspstat bit r/w
  1 sspstat bit ua
  0 sspstat bit bf

( ansel bits )

  6 ansel bit ans6
  5 ansel bit ans5
  4 ansel bit ans4
  3 ansel bit ans3
  2 ansel bit ans2
  1 ansel bit ans1
  0 ansel bit ans0
  
( adcon0 bits )

  7 adcon0 bit adcs1
  6 adcon0 bit adcs0
  5 adcon0 bit chs2
  4 adcon0 bit chs1
  3 adcon0 bit chs0
  2 adcon0 bit go//done
  0 adcon0 bit adon
  
( adcon1 bits )

  7 adcon1 bit adfm
  6 adcon1 bit adcs2
  5 adcon1 bit vcfg1
  4 adcon1 bit vcfg0
  
( ccp1con bits )

  5 ccp1con bit ccp1x
  4 ccp1con bit ccp1y
  3 ccp1con bit ccp1m3
  2 ccp1con bit ccp1m2
  1 ccp1con bit ccp1m1
  0 ccp1con bit ccp1m0

( pcon bits )

  1 pcon bit /por
  0 pcon bit /bor

( osccon bits )

  6 osccon bit ircf2
  5 osccon bit ircf1
  4 osccon bit ircf0
  3 osccon bit osts
  2 osccon bit iufs
  1 osccon bit scs1
  0 osccon bit scs0

( osctune bits )

  5 osctune bit tun5
  4 osctune bit tun4
  3 osctune bit tun3
  2 osctune bit tun2
  1 osctune bit tun1
  0 osctune bit tun0

\ ----------------------------------------------------------------------
\ Data and variables
\ ----------------------------------------------------------------------

meta

\ RAM

: create ( "name" -- ) create data-here , immediate does> (t-constant) ;

: , data-here t! data-here 1+ data-org ;

: c,          \ 8 bits system, "c," and "," are equivalent
    meta> ,
;

: allot data-here + dup data-org 1- data-check ;

: variable
    meta> create 1 allot
;

\ EEPROM

: eecreate
    create teehere , immediate
does>
    @ tcompile? if
	(literal)
    else
	state @ if
	    postpone literal macro?
	then
    then
;

: eeallot ( n -- ) teehere + to teehere ;

: eevariable eecreate 1 eeallot ;

: ee, ( b -- )
    teehere tee!
    teehere 1+ to teehere
;

: (sliteral) ( addr n -- ) ( run: -- eeaddr n )
    teehere (literal) dup (literal) 0 ?do dup c@ ee, char+ loop drop
;

: s" ( "ccc<quote>" -- ) ( run: -- eaddr n ) [char] " parse (sliteral) ;
: l" ( "ccc<quote>" -- ) ( run: -- eaddr n )
  [char] " parse 2 + (sliteral)                  \ "Keep VIM happy
  $d teehere 2 - tee!
  $a teehere 1- tee!
;

\ Flash

: align ;     \ 8 bits system, no alignment is needed

\ Misc

: [char] ( "name" -- ) ( run: -- ascii) char (literal) ;

\ ----------------------------------------------------------------------
\ Dictionary
\ ----------------------------------------------------------------------

host

\ In host memory, a word contains:
\    - address in target memory (1 cell)
\    - length in target memory (1 cell)
\    - address of previous word
\    - pointer to the name (counted string)
\    - depth of the code
\    - is W-TOS convention used (1 cell)
\    - is W-TOS return convention used (1 cell)
\ -1 means that length has not been computed

\ Store string as a counted string in here
: s, ( caddr n -- addr ) here >r dup c, 0 ?do dup c@ c, char+ loop drop r> ;

\ Store name (without consuming it) and return address of a counted string
: store-name ( "name" -- addr ) >in @ name s, swap >in ! ;

\ Accessors
: t-depth ( addr -- ) 4 cells + @ ;
: t-name ( addr -- ) 3 cells + @ count ;
: t-previous ( addr -- addr' ) 2 cells + @ ;
: t-length ( addr -- n ) 1 cells + @ ;

\ Code bank handling

variable current-cbank
variable prev-cbank

\ Compute code bank from an address
: cbank ( addr -- bank ) $b rshift ;

\ Mark current code bank as unknown
: no-cbank ( -- ) current-cbank @ prev-cbank ! -1 current-cbank ! ;

\ Mark current code bank as being current code location
: cbank-ok ( -- ) tcshere cbank current-cbank ! ;

: set-cbank-bit ( bank n -- )
    tuck 1 swap lshift and 0= 1+ 4 lshift             \ $10: bit set in bank
    over 1 swap lshift current-cbank @ and 0= 1+ or   \ $01: bit set in cbank
    case
	01 of 3 + pclath swap bcf endof
	10 of 3 + pclath swap bsf endof
	drop
    endcase
;

: set-cbank ( addr -- )
    \ If target is in bank 0, clear PCLATH
    cbank dup 0= if drop pclath clrf 0 current-cbank ! exit then
    \ If current known bank is invalid, everything is wrong
    current-cbank @ -1 = if dup invert current-cbank ! then
    \ If current address is in bank 0 or 1, clear one bit
    tcshere cbank 2 < if current-cbank @ $d and current-cbank ! then
    \ Change only the right bits
    dup 0 set-cbank-bit 1 set-cbank-bit
;

: manipulate-cbank ( addr -- )
    \ If cbank is -2 (main/isr), set PCLATH
    current-cbank @ -2 = if
	cbank 3 lshift ?dup if movlw pclath movwf else pclath clrf then
	no-cbank exit
    then
    \ No need to do anything when everything is in bank 0
    dup cbank tcshere cbank or 0= if drop exit then
    \ Set right bank if needed and truncate address to 11 bits
    dup cbank current-cbank @ <> if set-cbank else drop then
;

: adjust-cbank ( addr -- addr' ) dup manipulate-cbank dup literal! 7ff and ;

: l-goto ( addr -- ) adjust-cbank goto ;
: l-call ( addr -- )
    adjust-cbank call
    current-cbank @ if no-cbank then    \ A call in bank 0 holds 0 in PCLATH
;

\ Control of the word being currently defined

: reachable deadcode? unset no-opt ;
: unreachable deadcode? set cbank-ok ;

variable last-word
: last-addr ( -- ) last-word @ ;
: start-word ( -- ) here last-word ! ;
: compute-length ( -- ) tcshere last-addr @ - last-addr cell+ ! ;
: >host ( addr -- addr'|0 )
    last-addr
    begin
	dup while
	2dup @ = if nip exit then
	t-previous
    repeat
    nip ;
: compute-depth ( -- )
    0 last-addr @ last-addr t-length over + swap do
	i tcsdata@ 0= i tcs@ 3000 and 2000 = and if
	    i tcslit@ >host ?dup if
		t-depth
		i tcs@ 800 and 0= if 1+ then
		max
	    then
	then
    loop
    last-addr 4 cells + !
    ;
: end-word ( -- ) compute-length compute-depth ;

: (t-act) 
    tcompile? if
	dup 5 cells + @ if popw then
	dup @ l-call
	6 cells + @ if pushw then
    else
	@ state @ if
	    postpone literal
	then
    then
;

: t-dict ( w? addr -- )
    create last-addr start-word tcshere , -1 , , , -1 , , 0 ,
;

: t-header ( w? -- ) store-name t-dict reachable cbank-ok does> (t-act) ;

: return-in-w ( -- ) 1 last-addr 6 cells + ! ;

\ ----------------------------------------------------------------------
\ Compiler
\ ----------------------------------------------------------------------

meta

: ]asm postfix -tcompile also forth also picforth also picassembler ;
: asm[ +tcompile previous previous previous reachable ;

: code false t-header ]asm ;
: ::code true t-header ]asm ;

: nip meta> popw storew ;

: recurse last-addr @ l-call ;

host

: call-no-opt? lastcs 3800 and 2000 = ;
: short-call-address cs-unwind 7ff and ;

: const?-0 opt2? if
    lastcs 0180 = prevlastcs 0384 = and exit
  else false then ;

: const?-non-0 opt3? if
    lastcs 0080 = prevlastcs 0384 = and prevprevlastcs 3800 and 3000 = and
  else false then ;

: const?-inplace opt2? if
    lastcs 0080 = prevlastcs 3800 and 3000 = and
  else false then ;

: note? ( -- f ) opt? if tcshere 1- tnotes@ else false then ;

: const? note? if false else const?-0 const?-non-0 or const?-inplace or then ;

: kill-const
    const?-0 if cs-rewind2 literal? exit then
    const?-non-0 if cs-rewind3 literal? exit then
    cs-rewind2 literal? pop
;

: 2const?
  const? if
    kill-const const? if true else false then swap (literal)
  else
    false
  then ;
: kill-2const kill-const kill-const ;

: 3const?
  const? if
    kill-const 2const? if true else false then swap (literal)
  else
    false
  then ;

meta

: +
    const? if
	kill-const
	dup 0= if drop exit then
	const? if
	    kill-const + (literal)
	else
	    pushw? if
		popw addlw pushw
	    else
		dup 1 = if drop indf ,f incf exit then
		dup ff and ff = if drop indf ,f decf exit then
		movlw
		indf ,f addwf
	    then
	then
    else
	popw
	indf ,f addwf
    then
;

: -
    const? if
	kill-const negate (literal) meta> +
    else
	popw indf ,f subwf
    then
;

host

\ Defaut stack size is 16
10 value stack-size
: make-stack ( -- addr )
    stack-size meta> bank0 allot
    data-here
;

meta

: set-stack-size to stack-size ;

target

\ Temporary variables
udata
variable tmp1
variable tmp2
idata

host

\ Words to manipulate temporary variables

: w>tmp1 [ tmp1 ] literal movwf ;
: tmp1>w [ tmp1 ] literal ,w movf ;
: w>tmp2 [ tmp2 ] literal movwf ;
: tmp2>w [ tmp2 ] literal ,w movf ;

\ Words to manipulate fsr register

: w>fsr [ fsr ] literal movwf ;
: fsr>w [ fsr ] literal ,w movf ;

meta

import: w>tmp1   import: tmp1>w
import: w>tmp2   import: tmp2>w
import: w>fsr    import: fsr>w

host

\ Modules loaded on-demand. The current solution is not very efficient
\ but it works.

variable (!)-loaded
: (!)
    (!)-loaded @ dup if execute exit then
    1 abort" Please include libstore.fs in your application"
;

variable (@)-loaded
: (@)
    (@)-loaded @ dup if execute exit then
    1 abort" Please include libfetch.fs in your application"
;

variable (lshift)-loaded
: (lshift)
    (lshift)-loaded @ dup if execute exit then
    1 abort" Please include liblshift.fs in your application"
;

variable (rshift)-loaded
: (rshift)
    (rshift)-loaded @ dup if execute exit then
    1 abort" Please include librshift.fs in your application"
;

variable (cmove)-loaded
: (cmove)
    (cmove)-loaded @ dup if execute exit then
    1 abort" Please include libcmove.fs in your application"
;

variable current-bank

: rp0bsf ( -- )
    opt? if lastcs 1283 = if cs-rewind exit then then
    opt2? if lastcs ff00 and 3000 = prevlastcs 1283 = and if
	cs-unwind cs-rewind cs, exit
    then then
    reg-rp0 bsf
;

: rp1bsf ( -- )
    opt? if lastcs 1303 = if cs-rewind exit then then   \ rp1bcf
    opt2? if lastcs 1683 = prevlastcs 1303 = and if     \ rp1bcf rp0bsf
	cs-unwind cs-rewind cs, exit
    then then
    opt2? if lastcs 1283 = prevlastcs 1303 = and if     \ rp1bcf rp0bcf
	cs-unwind cs-rewind cs, exit
    then then
    opt2? if lastcs ff00 and 3000 = prevlastcs 1303 = and if
	cs-unwind cs-rewind cs, exit
    then then
    reg-rp1 bsf
;

: adjust-bank ( a -- a' )
    case dup 180 and dup current-bank !
	80  of rp0bsf endof
	100 of rp1bsf endof
	180 of rp0bsf rp1bsf endof
    endcase
    7f and
;

: rp0bcf ( -- ) reg-rp0 bcf ;
: rp1bcf ( -- ) reg-rp1 bcf ;

: restore-bank ( -- )
    case current-bank @
	80  of rp0bcf endof
	100 of rp1bcf endof
	180 of rp1bcf rp0bcf endof
    endcase
    0 current-bank !
;

: const-! ( addr -- )
    const? if
	kill-const dup 0= if
	    drop adjust-bank clrf restore-bank
	else
	    movlw adjust-bank movwf restore-bank
	then
    else
	meta> popw adjust-bank movwf restore-bank
    then
;

meta

import: adjust-bank   import: restore-bank

: !
    const? if
	kill-const const-!
    else
	(!)
    then
;

: @
    const? if
	kill-const ,w adjust-bank movf restore-bank meta> pushw
    else
	(@)
    then
;

: c@ meta> @
;

: c! meta> !
;

: swap
    const? if
      kill-const const? if
        \ Swap two literals on the host (such as s" or l" results)
        kill-const swap (literal) (literal) exit
      else
        (literal)
      then
    then
    popw
    indf ,w xorwf
    indf ,f xorwf
    indf ,w xorwf
    pushw
;

: over
  s" suspend-interrupts" evaluate
  popw w>tmp1 loadw w>tmp2 tmp1>w pushw tmp2>w pushw
  s" restore-interrupts" evaluate
;

: tuck
  s" suspend-interrupts" evaluate
  popw w>tmp1 loadw w>tmp2 tmp1>w storew tmp2>w pushw tmp1>w pushw
  s" restore-interrupts" evaluate
;

: 2dup
  s" suspend-interrupts" evaluate
  popw w>tmp2 loadw w>tmp1 tmp2>w pushw tmp1>w pushw tmp2>w pushw
  s" restore-interrupts" evaluate
;

: pick
  s" suspend-interrupts" evaluate
  popw w>tmp1 fsr ,f addwf indf ,w movf w>tmp2 tmp1>w fsr ,f subwf tmp2>w pushw
  s" restore-interrupts" evaluate
;

: +!
    const? if                           \ Address is constant
	kill-const
	const? if                       \ Number is constant
	    kill-const
	    dup 1 = if                  \ Number is 1
		drop adjust-bank ,f incf restore-bank
	    else dup ff and ff = if     \ Number is -1
		drop adjust-bank ,f decf restore-bank
	    else
		movlw adjust-bank ,f addwf restore-bank
	    then then
	else
	    popw adjust-bank addwf restore-bank
	then
    else
	meta> tuck @ + swap !
    then
;

: negate
    const? if kill-const negate (literal) exit then
    pushw? if popw 0 sublw pushw exit then
    indf ,f comf
    indf ,f incf
;

: invert
    const? if kill-const invert (literal) exit then
    pushw? if popw ff xorlw pushw exit then
    indf ,f comf
;

: cmove
  3const? if
    kill-const dup 4 <= if
      kill-2const rot 0 do dup ,f movf 1+ over movwf swap 1+ swap loop 2drop
    else
      (literal) (cmove)
    then
  else
    (cmove)
  then
;

host

: pop-value-w const? if kill-const movlw else popw then ;

meta

: 1+
    1 (literal) meta> +
;

: 1-
    1 (literal) meta> -
;

\ Flow control words

forth

: resolve ( faddr -- )
    dup cbank current-cbank !
    tcshere swap org reachable dup l-goto org reachable ;

meta

: ahead ( -- faddr ) tcshere adjust-cbank drop tcshere 0 goto unreachable ;

: w-or-indf ( opcode -- f ) dup ff and 80 = swap 80 and 0 = or ;
: complastcs ( mask -- f ) lastcs ff00 and = ;
: complastcs-f ( mask -- f ) complastcs lastcs w-or-indf and ;

meta

: get-const ( -- const )
    const? 0= if -1 abort" can only be used with a constant" then
    kill-const ;

: dup loadw pushw ;
: drop const? if kill-const drop else pop then ;
: 2drop pop pop ;

: -!
    get-const meta> negate
    (literal) meta> +!
;

: w-! get-const ,f subwf ;

: rrf! get-const adjust-bank ,f rrf restore-bank ;
: rlf! get-const adjust-bank ,f rlf restore-bank ;

: log2 ( n -- n' )
    7 for 1 lshift dup 100 = if drop i unloop exit then next drop -1 ;

: and
    const? if
	kill-const const? if
	    kill-const and (literal)
	else
	    pushw? if
		cs-rewind2 andlw pushw
	    else
		dup invert ff and log2 dup -1 <> if
		    indf swap bcf drop
		else
		    drop movlw indf ,f andwf
		then
	    then
	then
    else
	popw indf ,f andwf
    then
;

: and!
    const? if
	kill-const const? if
	    kill-const dup invert ff and log2 dup -1 <> if
		nip >r adjust-bank r> bcf restore-bank
	    else
		drop movlw ,f adjust-bank andwf restore-bank
	    then
	else
	    pop-value-w ,f adjust-bank andwf restore-bank
	then
    else
	meta> tuck @ and swap !
    then
;

: /and!
    const? if
	kill-const meta> invert literal and!
    else
	meta> swap invert swap and!
    then
;

: /and
    meta> invert and
;

: xor
    const? if
	kill-const const? if
	    kill-const xor (literal)
	else
	    pushw? if cs-rewind2 xorlw pushw
	    else movlw indf ,f xorwf then
	then
    else
	popw indf ,f xorwf
    then
;

: xor!
    const? if
	kill-const pop-value-w ,f adjust-bank xorwf restore-bank
    else
	meta> tuck @ xor swap !
    then
;

: or
    const? if
	kill-const const? if
	    kill-const or (literal)
	else
	    pushw? if
		cs-rewind2 iorlw pushw
	    else
		dup log2 dup -1 <> if
		    indf swap bsf drop
		else
		    drop
		    movlw indf ,f iorwf
		then
	    then
	then
    else
	popw indf ,f iorwf
    then
;

: or!
    const? if
	kill-const const? if
	    kill-const dup log2 dup -1 <> if
		nip >r adjust-bank r> bsf restore-bank
	    else
		drop movlw ,f adjust-bank iorwf restore-bank
	    then
	else
	    pop-value-w ,f adjust-bank iorwf restore-bank
	then
    else
	meta> tuck @ or swap !
    then
;

: invert!
    const? if
	kill-const ,f comf
    else
	meta> dup @ invert swap !
    then
;

\ Standard tests, using annotations to remove useless code if a branch is
\ used just after.

: 0=
    note-z >note
    clrw
    indf ,f iorwf
    z btfsc
    ff movlw
    indf movwf
    no-note
;

: 0<>
    note-/z >note
    loadw
    z btfss
    ff xorlw
    indf ,f iorwf
    no-note
;

: =
    meta> xor 0=
;

: <>
    meta> xor 0<>
;

: carry-set?
    \ This operation can arrive after a constant in case of a removed test
    const? if
      kill-const 0< load-literalw pushw meta> 0=
      exit
    then
    note-c >note
    clrw     \ Does not affect carry bit
    c btfsc
    ff addlw
    indf movwf
    no-note
;

: carry-clr?
    \ This operation can arrive after a constant in case of a removed test
    const? if
      kill-const 0< load-literalw pushw meta> 0<>
      exit
    then
    note-/c >note
    clrw
    c btfss
    ff addlw
    indf movwf
    no-note
;

: 0<
    80 (literal) meta> and 0<>
;
   
: <
    const? if
	kill-const negate popw addlw pushw carry-clr?
    else
	meta> - carry-clr?
    then
;

: >=
    const? if
	kill-const negate popw addlw pushw carry-set?
    else
	meta> - carry-set?
    then
;

: >
    meta> swap <
;

: <=
    meta> swap >=
;

\ Test if the last opcode did set the z bit.

: zbit?
    opt? 0= if false exit then
    3900 complastcs if true exit then            \ andlw
    0500 complastcs-f if true exit then          \ andwf
    0800 complastcs if true exit then            \ movlw
    3e00 complastcs if true exit then            \ addlw
    0700 complastcs-f if true exit then          \ addwf
    0100 complastcs if true exit then            \ clrw
    0900 complastcs-f if true exit then          \ comf
    0300 complastcs-f if true exit then          \ decf
    0a00 complastcs-f if true exit then          \ incf
    3800 complastcs if true exit then            \ iorlw
    0400 complastcs-f if true exit then          \ iorwf
    3c00 complastcs if true exit then            \ sublw
    0200 complastcs-f if true exit then          \ subwf
    3a00 complastcs if true exit then            \ xorlw
    0600 complastcs-f if true exit then          \ xorwf
    0800 complastcs-f if true exit then          \ movf
    false ;
 
: test-z-bit ( -- )
    zbit? 0= if 0 iorlw exit then
    \ If the last operation is a movf 0,w and the previous one sets the z bit
    \ and operates on the stack, the result is the same.
    opt? if lastcs 0800 = if
	cs-rewind zbit? if exit then
	\ No, restore the load
	0800 cs,
    then then
;

: test-structure ( -- faddr )
    cs-unwind tcshere manipulate-cbank cbank-ok cs,
    meta> ahead
    reachable
;

\ Is that equivalent to a bit-test sequence?

: bit-test? ( -- f )
    opt2? if lastcs ff00 and 3900 =
	     lastcs ff and log2 -1 <> and
	     prevlastcs ff00 and 0800 = and
	 else
	     false
	 then
;

\ Rewrite a bit-test sequence

: rewrite-bit-test ( -- addr bit )
    cs-unwind2 ff and swap ff and log2 ;

\ If we have decf 0,f then btfss 3,2, replace it with decfsz 0,f.
\ Idem for incf/incfsz.

: check-incdectos-btfsz ( -- )
    \ We are in a if, do not check opt2
    lastcs 1d03 = prevlastcs 0380 = and if
	cs-rewind2 indf ,f decfsz
    then
    lastcs 1d03 = prevlastcs 0a80 = and if
	cs-rewind2 indf ,f incfsz
    then
;

: c-z ( n -- n' ) [ note-c note-z or ] literal and ;

\ Optimize normalizations using annotations

: rewind-note ( -- note )
    note?
    begin
	\ Remove last normalization
	begin note? cs-rewind note-first and until
	\ Can another optimization be combined? (between 0= and 0<>)
	dup c-z note? c-z = if
	    note? note-invert and note-invert xor xor false
	else
	    true
	then
    until
    popw
;

: rewrite-note ( -- )
    rewind-note
    dup note-z and bit-test? and if
	>r rewrite-bit-test r> note-invert and if btfss else btfsc then
	check-incdectos-btfsz exit
    then
    dup note-c and if c else test-z-bit z then
    rot note-invert and if btfsc else btfss then
;

: if ( -- faddr )
    note? if
	rewrite-note
    else
	popw
	bit-test? if
	    rewrite-bit-test btfss check-incdectos-btfsz
	else
	    test-z-bit z btfsc
	then
    then
    test-structure
;

\ At resolve time, check whether we have something like:
\   btfsc ... or btfss ...
\   goto  0 \ unresolved yet
\   any instruction
\ If this is the case, this will be changed into:
\   btfss ... or btfsc ...
\   any instruction
\ and no further action will be taken.

: short-if? ( faddr -- faddr f )
    dup 2 + tcshere =
    prevlastcs 2800 = and
    prevprevlastcs 1800 and 1800 = and
;

\ Invert the conditional which was put last
: invert-last-btfsx ( -- ) cs-unwind 0400 xor cs, ;

: then ( faddr -- )
    dup cbank tcshere cbank <> abort" Bank switch over test"
    short-if? opt-allowed? and if
	drop reachable l-cs-unwind cs-rewind invert-last-btfsx
	check-incdectos-btfsz
	l-cs, reachable
    else
	resolve
    then ;

\ At resolve time, check whether we have something like:
\   btfsc ... or btfss ...
\   goto  0 \ unresolved yet
\ This would mean that we get a if with an else and nothing in between

: empty-if? ( faddr -- faddr f )
    dup 1 + tcshere =
    lastcs 2800 = and
    prevlastcs 1800 and 1800 = and
;

: else ( faddr1 -- faddr2 )
    empty-if? opt-allowed? and if
	reachable cs-unwind invert-last-btfsx cs, exit
    else
	meta> ahead
	swap resolve
    then
;

\ In a backward reference, we cannot assume that the bank will be properly
\ restored to its correct value. We could do that by remembering the
\ value it should have, but this may be an underoptimization as we may
\ need to explicitely call code in another bank just after the backward
\ reference.
: backref ( -- baddr ) tcshere no-opt no-cbank ;

: begin ( -- 0 baddr ) 0 backref ;

: again ( 0 baddr -- ) l-goto drop unreachable ;

: while ( 0 i*x baddr -- 0 i*x faddr baddr )
    meta> if
    swap
;

: repeat ( 0 i*x baddr -- )
    0 swap meta> again
    begin dup while meta> then
    repeat drop
;

: until ( 0 baddr -- )
    meta> 0= while repeat
;

: v-for ( -- vaddr 0 baddr ) ( runtime: n -- )
    get-const dup 180 and abort" needs a variable in bank 0"
    dup const-! meta> begin
;

: v-next ( vaddr 0 baddr -- ) ( runtime: -- )
    rot
    \ Set bank before the test so that we do have a
    \ short jump
    over adjust-cbank drop over cbank current-cbank !
    ,f decfsz meta> again
    reachable
;

: bit-set get-const get-const adjust-bank swap bsf restore-bank ;
: bit-clr get-const get-const adjust-bank swap bcf restore-bank ;
: bit-toggle
    1 get-const lshift get-const swap movlw ,f adjust-bank xorwf restore-bank ;
: bit-set? 1 get-const lshift get-const meta> (literal) @ (literal) and ;
: bit-clr?
    meta> bit-set? 0=
;
: bit-mask 1 get-const get-const drop lshift (literal) ;
    
: >input get-const get-const 80 or (literal) (literal) bit-set ;
: >output get-const get-const 80 or (literal) (literal) bit-clr ;
    
\ Aliases, more readable for port manipulation

: high
    meta> bit-set
;
: low
    meta> bit-clr
;
: high?
    meta> bit-set?
;
: low?
    meta> bit-clr?
;

: toggle
    meta> bit-toggle
;

: pin-a ( n -- ) porta bit ;
: pin-b ( n -- ) portb bit ;
: pin-c ( n -- ) portc bit ;
: pin-d ( n -- ) portd bit ;
: pin-e ( n -- ) porte bit ;
    
: rlf-tos indf ,f rlf ;
: rrf-tos indf ,f rrf ;
: swapf-tos indf ,f swapf ;

: lshift
    const? if
	kill-const const? if
	    kill-const swap lshift (literal)
	else
	    dup 0 = if drop exit then
	    dup 1 = if drop c bcf indf ,f rlf exit then
	    dup
	    dup 4 >= if
		indf ,f swapf
		4 -
	    then
	    0 ?do indf ,f rlf loop
	    100 1 rot lshift - (literal) meta> and
	then
    else
	(lshift)
    then
;

: rshift
    const? if
	kill-const const? if
	    kill-const swap rshift (literal)
	else
	    dup 0 = if drop exit then
	    dup 1 = if drop c bcf indf ,f rrf exit then
	    dup
	    dup 4 >= if
		indf ,f swapf
		4 -
	    then
	    0 ?do indf ,f rrf loop
	    $ff swap rshift (literal) meta> and
	then
    else
	(rshift)
    then
;

: 2* const? if kill-const 2* (literal) else c bcf indf ,f rlf then ;
: 2/ const? if kill-const 2/ (literal) else indf ,w rlf indf ,f rrf then ;

: clrwdt clrwdt ;
: sleep sleep ;

\ Definitions

: : false t-header +tcompile ;
: :: true t-header +tcompile pushw ;

host

\ A call followed by a return can always be replaced by a goto, regardless
\ of the opt? value. However, if opt? is true, the following code becomes
\ unreachable.

: call>goto short-call-address goto prev-cbank @ current-cbank ! ;
: (return)
    call-no-opt? if call>goto opt? if unreachable then then
    return ;
: (exit) (return) unreachable ;

meta

: exit (exit) ;
: ; (exit) -tcompile end-word ;

: label: ( "name" -- ) create tcshere , does> @ ;

: ' ( "name" -- xt )
    ' >body @ ;

\ Reset the device -- caution must be taken: not everything will return
\ in default state, only the program counter and the stacks (well, only
\ the data stack, but since the return stack is a circular buffer, it has
\ the same effect for any correct program).
: reset ( -- ) 0 l-goto unreachable ;

\ Dummy interrupt handling routines (overloaded when picisr.fs is loaded)
: enable-interrupts ( -- ) ;
: disable-interrupts ( -- ) ;
: suspend-interrupts ( -- ) ;
: restore-interrupts ( -- ) ;

picasm

: end-code previous previous previous end-word ;

host

variable init-chain-last

: add-to-init-chain ( xt -- )
    here swap , init-chain-last @ , init-chain-last ! ;

: init-chain ( -- )
    init-chain-last @ begin dup while dup @ execute cell+ @ repeat drop ;

: init-tdata-slot ( n f a -- n f' )
    adjust-bank
    >r over if
	if dup (literal) popw then r> movwf false
    else
	r> clrf drop false
    then
;

: init-tdata-value ( n -- )
    true
    data-here data-low ?do
	i initialized? if over i t@ ff and = if i init-tdata-slot then then
    loop
    0= if restore-bank then
    drop
;

: init-tdata ( -- )
    current-area @
    ['] idata >body @ current-area !
    begin
	current-area @ while
	100 0 do i init-tdata-value loop
	data-previous current-area !
    repeat
    current-area !
;

: init-picforth
    s" : (init-picforth)" evaluate
    make-stack movlw fsr movwf
    init-tdata
    init-chain
    end-word
;

\ Set a goto to the current address at addr
: set-vector ( addr -- )
    no-cbank tcshere swap org
    reachable -2 current-cbank ! dup l-goto
    org ;

meta

\ Address of main program
: main ( -- ) 0 set-vector init-picforth ;

\ ----------------------------------------------------------------------
\ Tables
\ ----------------------------------------------------------------------

host

: element-ftable ( n -- ) retlw ;

: end-ftable ( start -- )
    $ff00 and tcshere $ff00 and <> abort" flash table crosses page boundaries"
    unreachable meta> ;
;

: element-eetable ( n -- )
    meta> ee,
;

: element-table ( n -- )
    meta> ,
;

: resolve-eetable ( addr -- )
    @ (literal) meta> +
    s" ee@" evaluate
;

: resolve-table ( addr -- )
    @ (literal) meta> +
    s" @" evaluate
;

meta

: t, ( endxt elemxt n -- endxt elemxt ) over execute ;

: table> ( endxt elemxt -- endxt elemxt )
    >r depth >r source >in @ /string evaluate depth 2r> rot swap - 1- for
      i 1+ roll over execute
    next postpone \
;

: end-table ( endxt elemxt -- ) drop execute ;

: ftable ( "name" -- start endxt elemxt )
    reachable tcshere ['] end-ftable ['] element-ftable
    meta> :: >w
    return-in-w pcl ,f addwf
    -tcompile
;

: eetable ( "name" -- endxt elemxt )
    ['] noop ['] element-eetable
    create teehere , does> resolve-eetable
;

: table ( "name" -- endxt elemxt )
    ['] noop ['] element-table
    create data-here , does> resolve-table
;

\ ----------------------------------------------------------------------
\ Outer interpreter
\ ----------------------------------------------------------------------

host

\ This is needed as numbers cannot have special treatments on a
\ regular Forth without building another outer interpreter.

: throw-unknown ( caddr n -- )
    ." Unknown word: " type cr abort
;

: do-number ( n -- |n )
    state @ if postpone literal macro? exit then
    tcompile? if [ meta ] (literal) [ host ] then
;

: >number ( caddr n -- )
    2dup 2>r snumber? dup 0= if drop 2r> throw-unknown then
    2r> 2drop
    1 = if do-number then
    do-number
;

: parser ( caddr n -- )
    2dup sfind
    dup 0= if drop >number exit then
    2swap 2drop 1 = state @ 0= or if execute else compile, then
;

: interpret ( -- )
    begin ?stack name dup while parser repeat
    2drop ;

: (picquit) ( -- ) begin .status cr query interpret prompt again ;

' (picquit) is 'quit

\ ----------------------------------------------------------------------
\ Make use of the newly defined interpreter when including files
\ ----------------------------------------------------------------------

: | ;

include input.fs
include require.fs

meta

\ Add the capability to include a file

import: include     import: included
import: require     import: required

: needs require ;

\ ----------------------------------------------------------------------
\ Import conditional compilation definitions
\ ----------------------------------------------------------------------

meta

import: [if]      import: [else]        import: [then]
import: [ifdef]   import: [ifundef]

\ ----------------------------------------------------------------------
\ Dump of hexadecimal data
\ ----------------------------------------------------------------------

host

-1 value fd
: fd? fd -1 <> ;
: emit-fd fd? if fd emit-file throw else emit then ;
: type-fd fd? if fd write-file throw else type then ;
create crlf a c, d c,
: cr-fd fd? if a emit-fd else cr then ;

variable cks
: +cks cks +! ;
: hex8. dup +cks s>d <# # # #> type-fd ;
: hex16. dup 8 rshift hex8. ff and hex8. ;
: le-hex16. dup ff and hex8. 8 rshift hex8. ;
: mem-dump tcs@ le-hex16. ;
: start-line [char] : emit-fd 0 cks ! ;
: end-line cks @ ff and negate hex8. cr-fd ;
: line-dump ( addr -- )
    start-line 10 hex8. dup 2* hex16. 0 hex8.
    7 for dup mem-dump 1+ next drop end-line ;
: code-dump
    0 begin dup tcshere < while dup line-dump 8 + repeat drop ;
: ee-dump tee@ le-hex16. ;
: eeline-dump ( addr -- )
    start-line 10 hex8. dup 2100 + 2* hex16. 0 hex8.
    7 for dup ee-dump 1+ next drop end-line ;
: eeprom-dump
    0 begin dup teehere < while dup eeline-dump 8 + repeat drop ;
: end-dump s" :00000001FF" type-fd cr-fd ;
: config-dump
    start-line 4 hex8. 400E hex16. 0 hex8. configword-1 @ le-hex16. configword-2 @ le-hex16. end-line ;
: dump hex code-dump config-dump eeprom-dump end-dump ;

: dump-file ( caddr a -- )
    w/o create-file throw to fd dump fd close-file throw ;

: file-dump ( "name" -- ) name dump-file ;

meta

import: file-dump
import: bye

\ ----------------------------------------------------------------------
\ PIC disassembler
\ ----------------------------------------------------------------------

forth

\ Printable output

: .tab 9 emit ;
: (.op) ( caddr n -- ) .tab type .tab ;
: .op" postpone s" postpone (.op) ; immediate \ "
: .lit ." 0x" ff and s>d <# # # #> type ;
: .comment .tab ." ; " ;
: .char
  dup .lit space
  dup $20 < if 3 spaces else [char] [ emit emit [char] ] emit then ;
: .file ." 0x" 7f and s>d <# # # #> type ;
: .4const s>d <# # # # # #> type ;
: .4hexconst ." 0x" .4const ;
: .3const s>d <# # # # #> type ;
: .3hexconst ." 0x" .3const ;
: .addr dup 1000 >= if .4hexconst else .3hexconst then ;
: .bconst [char] 0 + emit ;
: .filewf dup .file ." ," 80 and if ." f" else ." w" then ;

\ Unknown opcode

: dis-unknown ( w -- ) .op" .dw" .4hexconst ;

\ Byte-oriented file register operations

: dis-0 ( w -- )
    dup 80 and if .op" movwf" .file exit then
    dup 1f and 0= if .op" nop" drop exit then
    dup 64 = if drop .op" clrwdt" exit then
    dup 9 = if drop .op" retfie" exit then
    dup 8 = if drop .op" return" exit then
    dup 63 = if drop .op" sleep" exit then
    dis-unknown
;

: dis-1 ( w -- )
    dup 80 and if .op" clrf" .file exit then
    .op" clrw" drop
;

: dis-fileop ( w -- )
    case dup f00 and
	000 of dis-0 endof
	100 of dis-1 endof
	200 of .op" subwf" .filewf endof
	300 of .op" decf" .filewf endof
	400 of .op" iorwf" .filewf endof
	500 of .op" andwf" .filewf endof
	600 of .op" xorwf" .filewf endof
	700 of .op" addwf" .filewf endof
	800 of .op" movf" .filewf endof
	900 of .op" comf" .filewf endof
	a00 of .op" incf" .filewf endof
	b00 of .op" decfsz" .filewf endof
	c00 of .op" rrf" .filewf endof
	d00 of .op" rlf" .filewf endof
	e00 of .op" swapf" .filewf endof
	f00 of .op" incfsz" .filewf endof
    endcase
;

\ Bit-oriented file register operations

: dis-bitop ( w -- )
    case dup c00 and
	000 of .op" bcf" endof
	400 of .op" bsf" endof
	800 of .op" btfsc" endof
	c00 of .op" btfss" endof
    endcase
    dup .file ." ," 380 and 7 rshift .bconst
;

\ Control operations

: .addr-name ( addr -- )
    last-addr
    begin
	dup while
	2dup @ >= if
	    dup t-name type 2dup @ - ?dup if
		."  + " .addr 2drop
	    else
		t-depth ?dup if
		    ."  (rs depth: " .bconst ." )"
		then
		drop
	    then
	    exit
	then
	t-previous
    repeat
    2drop ;

: dis-control ( addr w -- )
    dup 800 and if .op" goto" else .op" call" then
    7ff and .addr .comment tcslit@ dup .addr-name
    dup 7ff > if ."  (" .addr ." )" else drop then
;

\ Literal operations

: dis-lit ( w -- )
    dup e00 and e00 = if .op" addlw" .lit exit then
    dup f00 and 900 = if .op" andlw" .lit exit then
    dup f00 and 800 = if .op" iorlw" .lit exit then
    dup c00 and 0= if .op" movlw" .lit exit then
    dup c00 and 400 = if .op" retlw" .lit exit then
    dup e00 and c00 = if .op" sublw" .lit exit then
    dup f00 and a00 = if .op" xorlw" .lit exit then
    3000 or dis-unknown
;

\ Disassemble word

: dis-csdata ( w -- )
    .op" data " dup .lit .comment dup 7 rshift .char space $7f and .char
;

: dis-v ( addr w -- )
    over tcsdata@ if nip dis-csdata cr exit then
    case dup 3000 and
	0000 of nip dis-fileop endof
	1000 of nip dis-bitop endof
	2000 of dis-control endof
	3000 of nip dis-lit endof
    endcase
    cr
;

: dis-a ( addr -- )
    dup >host ?dup if
	.comment ." name: " dup t-name type cr
	.comment ." max return-stack depth: " t-depth .bconst cr
    then
    dup .4hexconst .tab dup tcs@ dup .4const dis-v ;

meta

: see ( "name" -- ) cr ' >body 2@ over 0 do dup dis-a 1+ loop drop ;

: words ( -- )
    cr last-addr
    begin dup while dup t-name type space t-previous repeat
    cr ;

: map ( -- )
    last-addr
    begin
	dup while
	dup @ .addr .tab dup t-depth .bconst .tab dup t-name type .tab 
	t-previous cr
    repeat
;

: dis ( -- ) tcshere 0 ?do i used? if i dis-a then loop ;

: unsupported create
  does> drop ." Error: nsupported directive for this architecture" cr quit ;
: unsupported2 unsupported unsupported ;
: unsupported3 unsupported2 unsupported ;

meta

: pic16f88 s" include pic16f88.inc" evaluate ;
: pic16f87x s" include pic16f87x.inc" evaluate ;

\ ----------------------------------------------------------------------
\ Switch into picforth parser mode from now on
\ ----------------------------------------------------------------------

target

\ Reserve space for jump to main (4 bytes, because of the need to erase
\ 4 bytes at a time on 16F8xxA devices)

4 org
