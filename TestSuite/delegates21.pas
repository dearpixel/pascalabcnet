﻿procedure pr1(act: (array of integer)-> ());
begin 
  act(Arr(1,2,3));
end;
var l: integer;

begin 
  pr1(x-> begin l := x.Length end);
  assert(l = 3);
end.