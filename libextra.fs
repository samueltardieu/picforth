\ Extra words requiring more temporary variables

variable extra1
variable extra2

host

: w>extra1 [ extra1 ] literal movwf ;
: extra1>w [ extra1 ] literal ,w movf ;
: w>extra2 [ extra2 ] literal movwf ;
: extra2>w [ extra2 ] literal ,w movf ;

meta

import: w>extra1   import: extra1>w
import: w>extra2   import: extra2>w

: rot
  s" suspend-interrupts" evaluate
  popw w>extra1 popw w>tmp2 loadw w>tmp1 tmp2>w storew extra1>w pushw tmp1>w pushw
  s" restore-interrupts" evaluate
;

: -rot
  s" suspend-interrupts" evaluate
  popw w>extra1 popw w>tmp2 loadw w>tmp1 extra1>w storew tmp1>w pushw tmp2>w pushw
  s" restore-interrupts" evaluate
;

: 2swap
  s" suspend-interrupts" evaluate
  popw w>extra2 popw w>extra1 popw w>tmp2 loadw w>tmp1 extra1>w storew extra2>w pushw tmp1>w pushw tmp2>w pushw
  s" restore-interrupts" evaluate
;

target