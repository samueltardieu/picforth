variable checksum
variable dcc-high
: x 9 and dup if 1+ then ;
: y dup checksum xor! dcc-high ! ;