#! /usr/local/bin/perl
#

$pre = 0;
$list = 0;
$inp = 0;

sub start_p {
  unless ($inp) {
    $inp = 1;
    print "<p>\n";
  }
}

sub stop_p {
  if ($inp) {
    $inp = 0;
    print "</p>\n";
  }
}

sub start_pre {
  unless ($pre) {
    $pre = 1;
    print "<pre>\n";
  }
}

sub stop_pre {
  if ($pre) {
    $pre = 0;
    print "</pre>\n";
  }
  if ($list) {
    $list = 0;
    print "</li></ul>\n";
  }
}

sub transform {
  my ($s) = @_;
  $s =~ s/</&lt;/g;
  $s =~ s/>/&gt;/g;
  unless ($pre) {
    $s =~ s,"([^"]+)",<code>$1</code>,g;     # "
  }
  return $s;
}

while (<>) {
  chomp;
  next if (/^-\*- outline/);
  if (/^\* (.*)/) {
    print "<h2>" . transform ($1) . "</h2>\n";
  } elsif (/^--- (.*)/) {
    $t = $1;
    $t =~ s/\$[^:]*: //;
    $t =~ s/ \$//;
    print "<h1>" . transform ($t) . "</h1>\n";
  } elsif (/^\*\* (.*)/) {
    print "<h3>" . transform ($1) . "</h3>\n";
  } elsif (/^\*\*\* (.*)/) {
    print "<h4>" . transform ($1) . "</h4>\n";
  } elsif (/^ / && $pre) {
    print transform ($_) . "\n";
  } elsif (/^ +[-+] (.*)/ && $list) {
    print "</li>\n<li>" . transform ($1) . "\n";
  } elsif (/^ +[-+] (.*)/) {
    stop_p ();
    print "<ul>\n<li>" . transform ($1) . "\n";
    $list = 1;
  } elsif (/^ +(.*)/ && $list) {
    print transform ($1) . "\n";
  } elsif (/^ /) {
    stop_p ();
    start_pre ();
    print transform ($_) . "\n";
  } elsif (/^$/ && $inp) {
    stop_p ();
  } elsif (/^$/) {
    stop_pre ();
  } else {
    stop_pre ();
    start_p ();
    print transform ($_) . "\n";
  }
}
stop_p ();
stop_pre ();
